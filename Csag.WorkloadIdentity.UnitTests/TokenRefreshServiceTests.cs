namespace Csag.WorkloadIdentity.UnitTests
{
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Internal;
    using Csag.WorkloadIdentity.Internal.Services;
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Time.Testing;
    using NSubstitute;
    using NSubstitute.Core;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="TokenRefreshService"/> class.
    /// </summary>
    public class TokenRefreshServiceTests
    {
        /// <summary>
        /// The instant at which every test starts.
        /// </summary>
        private static readonly DateTimeOffset StartTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// The refresh-ahead window the service under test is configured with.
        /// </summary>
        private static readonly TimeSpan RefreshAheadWindow = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The lifetime of the tokens the refreshers hand out unless a test says otherwise.
        /// </summary>
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

        /// <summary>
        /// The wait before the service retries after its first failed refresh.
        /// </summary>
        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(5);

        /// <summary>
        /// A short real-time pause that lets a wrongly scheduled refresh show up before a negative assertion.
        /// </summary>
        private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// The longest real time a test waits for the service to react.
        /// </summary>
        private static readonly TimeSpan ReactionTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The controllable clock the service under test schedules on.
        /// </summary>
        private readonly TimerCountingTimeProvider timeProvider = new(StartTime);

        /// <summary>
        /// The substituted Azure SQL token provider, which also supports forced refresh.
        /// </summary>
        private readonly IAzureSqlTokenProvider azureSqlTokenProvider = Substitute.For<IAzureSqlTokenProvider, ITokenRefresher>();

        /// <summary>
        /// The substituted Blob Storage token provider, which also supports forced refresh.
        /// </summary>
        private readonly IBlobStorageTokenProvider blobStorageTokenProvider = Substitute.For<IBlobStorageTokenProvider, ITokenRefresher>();

        /// <summary>
        /// Counts the refreshes the service requested from the Azure SQL provider.
        /// </summary>
        private readonly RefreshCounter azureSqlRefreshes = new();

        /// <summary>
        /// Counts the refreshes the service requested from the Blob Storage provider.
        /// </summary>
        private readonly RefreshCounter blobStorageRefreshes = new();

        /// <summary>
        /// Verifies that the service refreshes once at start and again exactly when the refresh-ahead point is reached.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_RefreshedToken_When_RefreshAheadPointIsReached_Then_RefreshesAgain()
        {
            // Arrange
            SetupRefresh(this.azureSqlTokenProvider, this.azureSqlRefreshes, _ => this.timeProvider.GetUtcNow() + TokenLifetime);
            using var service = this.CreateService();
            await service.StartAsync(CancellationToken.None);
            await this.WaitForScheduledRefreshAsync(1);

            // Act
            this.timeProvider.Advance(TokenLifetime - RefreshAheadWindow - TimeSpan.FromSeconds(1));
            await Task.Delay(SettleTime);
            var refreshesBeforeRefreshPoint = this.azureSqlRefreshes.Count;
            this.timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => this.azureSqlRefreshes.Count >= 2);

            // Assert
            refreshesBeforeRefreshPoint.ShouldBe(1);
            this.azureSqlRefreshes.Count.ShouldBe(2);
            await service.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// Verifies that a token shorter than the refresh-ahead window is refreshed after half its lifetime rather
        /// than immediately.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_TokenShorterThanRefreshAheadWindow_When_HalfItsLifetimeElapses_Then_RefreshesWithoutLooping()
        {
            // Arrange
            var lifetime = TimeSpan.FromMinutes(2);
            SetupRefresh(this.azureSqlTokenProvider, this.azureSqlRefreshes, _ => this.timeProvider.GetUtcNow() + lifetime);
            using var service = this.CreateService();
            await service.StartAsync(CancellationToken.None);
            await this.WaitForScheduledRefreshAsync(1);

            // Act
            this.timeProvider.Advance(TimeSpan.FromSeconds(59));
            await Task.Delay(SettleTime);
            var refreshesBeforeHalfLifetime = this.azureSqlRefreshes.Count;
            this.timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => this.azureSqlRefreshes.Count >= 2);

            // Assert
            refreshesBeforeHalfLifetime.ShouldBe(1);
            this.azureSqlRefreshes.Count.ShouldBe(2);
            await service.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// Verifies that a failed refresh is retried after the initial retry delay and the service keeps running.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_FailingRefresh_When_RetryDelayElapses_Then_RetriesRefresh()
        {
            // Arrange
            SetupRefresh(
                this.azureSqlTokenProvider,
                this.azureSqlRefreshes,
                _ => throw new InvalidOperationException("refresh failed"),
                _ => this.timeProvider.GetUtcNow() + TokenLifetime);
            using var service = this.CreateService();
            await service.StartAsync(CancellationToken.None);
            await this.WaitForScheduledRefreshAsync(1);

            // Act
            this.timeProvider.Advance(InitialRetryDelay - TimeSpan.FromSeconds(1));
            await Task.Delay(SettleTime);
            var refreshesBeforeRetryPoint = this.azureSqlRefreshes.Count;
            this.timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => this.azureSqlRefreshes.Count >= 2);

            // Assert
            refreshesBeforeRetryPoint.ShouldBe(1);
            this.azureSqlRefreshes.Count.ShouldBe(2);
            service.ExecuteTask.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
            await service.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// Verifies that stopping the service while it waits for the next refresh completes it without an error.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_RunningService_When_Stopped_Then_CompletesWithoutError()
        {
            // Arrange
            SetupRefresh(this.azureSqlTokenProvider, this.azureSqlRefreshes, _ => this.timeProvider.GetUtcNow() + TokenLifetime);
            using var service = this.CreateService();
            await service.StartAsync(CancellationToken.None);
            await this.WaitForScheduledRefreshAsync(1);

            // Act
            await service.StopAsync(CancellationToken.None);

            // Assert
            service.ExecuteTask.ShouldNotBeNull().Status.ShouldBe(TaskStatus.RanToCompletion);
            this.azureSqlRefreshes.Count.ShouldBe(1);
        }

        /// <summary>
        /// Verifies that the service does nothing when background refresh is disabled.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_BackgroundRefreshDisabled_When_Started_Then_DoesNotRefresh()
        {
            // Arrange
            SetupRefresh(this.azureSqlTokenProvider, this.azureSqlRefreshes, _ => this.timeProvider.GetUtcNow() + TokenLifetime);
            using var service = this.CreateService(enableBackgroundRefresh: false);

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask.ShouldNotBeNull().WaitAsync(ReactionTimeout);

            // Assert
            service.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
            this.azureSqlRefreshes.Count.ShouldBe(0);
        }

        /// <summary>
        /// Verifies that a consumer-substituted token provider without forced-refresh support is skipped instead of
        /// failing the service.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_TokenProviderWithoutRefreshSupport_When_Started_Then_CompletesWithoutError()
        {
            // Arrange
            var plainProvider = Substitute.For<IAzureSqlTokenProvider>();
            using var service = this.CreateService(azureSqlProvider: plainProvider);

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask.ShouldNotBeNull().WaitAsync(ReactionTimeout);

            // Assert
            service.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
            await plainProvider.DidNotReceiveWithAnyArgs().GetAzureSqlAccessTokenAsync(CancellationToken.None);
            await plainProvider.DidNotReceiveWithAnyArgs().GetAccessTokenAsync(CancellationToken.None);
        }

        /// <summary>
        /// Verifies that only the providers of configured resources are resolved: the Blob Storage provider is not
        /// registered here, and the service must not ask for it.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_OnlyAzureSqlConfigured_When_Started_Then_RefreshesAzureSqlWithoutResolvingBlobStorage()
        {
            // Arrange
            SetupRefresh(this.azureSqlTokenProvider, this.azureSqlRefreshes, _ => this.timeProvider.GetUtcNow() + TokenLifetime);
            using var service = this.CreateService();

            // Act
            await service.StartAsync(CancellationToken.None);
            await this.WaitForScheduledRefreshAsync(1);

            // Assert
            service.ExecuteTask.ShouldNotBeNull().IsFaulted.ShouldBeFalse();
            this.azureSqlRefreshes.Count.ShouldBe(1);
            await service.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// Verifies that two configured resources are refreshed by independent loops, each at its own refresh-ahead point.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_TwoConfiguredResources_When_TheirRefreshPointsAreReached_Then_RefreshesEachIndependently()
        {
            // Arrange
            var blobStorageLifetime = TimeSpan.FromMinutes(40);
            SetupRefresh(this.azureSqlTokenProvider, this.azureSqlRefreshes, _ => this.timeProvider.GetUtcNow() + TokenLifetime);
            SetupRefresh(this.blobStorageTokenProvider, this.blobStorageRefreshes, _ => this.timeProvider.GetUtcNow() + blobStorageLifetime);
            using var service = this.CreateService(configureBlobStorage: true);
            await service.StartAsync(CancellationToken.None);
            await this.WaitForScheduledRefreshAsync(2);

            // Act
            this.timeProvider.Advance(blobStorageLifetime - RefreshAheadWindow);
            await WaitUntilAsync(() => this.blobStorageRefreshes.Count >= 2);
            await Task.Delay(SettleTime);
            var azureSqlRefreshesAfterBlobStorageRefresh = this.azureSqlRefreshes.Count;
            this.timeProvider.Advance(TokenLifetime - blobStorageLifetime);
            await WaitUntilAsync(() => this.azureSqlRefreshes.Count >= 2);

            // Assert
            azureSqlRefreshesAfterBlobStorageRefresh.ShouldBe(1);
            this.azureSqlRefreshes.Count.ShouldBe(2);
            this.blobStorageRefreshes.Count.ShouldBe(2);
            await service.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// Waits, in real time, until the condition holds or the reaction timeout elapses.
        /// </summary>
        /// <param name="condition">The condition to wait for.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + ReactionTimeout;
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }
        }

        /// <summary>
        /// Configures a provider's refresher to produce the given results in order, repeating the last one, and
        /// counts every request.
        /// </summary>
        /// <param name="tokenProvider">The substituted provider, which also implements <see cref="ITokenRefresher"/>.</param>
        /// <param name="counter">Receives every request.</param>
        /// <param name="results">The results to produce; a result may throw to simulate a failed refresh.</param>
        private static void SetupRefresh(IAccessTokenProvider tokenProvider, RefreshCounter counter, params Func<CallInfo, DateTimeOffset>[] results)
        {
            var counted = results
                .Select(result => new Func<CallInfo, DateTimeOffset>(call =>
                {
                    counter.Increment();
                    return result(call);
                }))
                .ToArray();

            ((ITokenRefresher)tokenProvider).RefreshAsync(Arg.Any<CancellationToken>())
                .Returns(counted[0], counted.Skip(1).ToArray());
        }

        /// <summary>
        /// Waits until the service has scheduled the given number of refreshes (or retries) on the fake clock, so
        /// that advancing the clock afterwards is guaranteed to reach those schedules.
        /// </summary>
        /// <param name="scheduledRefreshes">The number of schedules to wait for, counted from the start of the service.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task WaitForScheduledRefreshAsync(int scheduledRefreshes)
        {
            await WaitUntilAsync(() => this.timeProvider.TimersCreated >= scheduledRefreshes);
            this.timeProvider.TimersCreated.ShouldBe(scheduledRefreshes);
        }

        /// <summary>
        /// Creates the service under test with the Azure SQL resource configured and its provider registered.
        /// </summary>
        /// <param name="enableBackgroundRefresh">Whether background refresh is enabled in the options.</param>
        /// <param name="configureBlobStorage">Whether the Blob Storage resource is configured and its provider registered too.</param>
        /// <param name="azureSqlProvider">The Azure SQL provider to register, or <see langword="null"/> for the refreshable substitute.</param>
        /// <returns>The service.</returns>
        private TokenRefreshService CreateService(bool enableBackgroundRefresh = true, bool configureBlobStorage = false, IAzureSqlTokenProvider? azureSqlProvider = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(azureSqlProvider ?? this.azureSqlTokenProvider);
            if (configureBlobStorage)
            {
                services.AddSingleton(this.blobStorageTokenProvider);
            }

            var options = Options.Create(new WorkloadIdentityOptions
            {
                AzureSql = new WorkloadIdentityResourceOptions(),
                BlobStorage = configureBlobStorage ? new WorkloadIdentityResourceOptions() : null,
                RefreshAheadWindow = RefreshAheadWindow,
                EnableBackgroundRefresh = enableBackgroundRefresh,
            });

            return new TokenRefreshService(services.BuildServiceProvider(), options, this.timeProvider, NullLogger<TokenRefreshService>.Instance);
        }

        /// <summary>
        /// A thread-safe counter of the refreshes requested from one provider.
        /// </summary>
        private sealed class RefreshCounter
        {
            /// <summary>
            /// The number of refreshes so far.
            /// </summary>
            private int count;

            /// <summary>
            /// Gets the number of refreshes so far.
            /// </summary>
            public int Count => Volatile.Read(ref this.count);

            /// <summary>
            /// Records one refresh.
            /// </summary>
            public void Increment()
            {
                Interlocked.Increment(ref this.count);
            }
        }

        /// <summary>
        /// A fake clock that also counts the timers created on it. The service starts its wait for the next refresh
        /// by creating a timer, so the count tells a test when advancing the clock will reach that wait.
        /// </summary>
        private sealed class TimerCountingTimeProvider : FakeTimeProvider
        {
            /// <summary>
            /// The number of timers created so far.
            /// </summary>
            private int timersCreated;

            /// <summary>
            /// Initializes a new instance of the <see cref="TimerCountingTimeProvider"/> class.
            /// </summary>
            /// <param name="startDateTime">The initial time.</param>
            public TimerCountingTimeProvider(DateTimeOffset startDateTime)
                : base(startDateTime)
            {
            }

            /// <summary>
            /// Gets the number of timers created so far.
            /// </summary>
            public int TimersCreated => Volatile.Read(ref this.timersCreated);

            /// <inheritdoc />
            public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            {
                var timer = base.CreateTimer(callback, state, dueTime, period);
                Interlocked.Increment(ref this.timersCreated);
                return timer;
            }
        }
    }
}
