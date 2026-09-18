namespace Csag.WorkloadIdentity.UnitTests
{
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Internal;
    using Csag.WorkloadIdentity.Internal.Services;
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Time.Testing;
    using NSubstitute;
    using NSubstitute.Core;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="AzureSqlTokenRefreshService"/> class.
    /// </summary>
    public class AzureSqlTokenRefreshServiceTests
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
        /// The lifetime of the tokens the refresher hands out unless a test says otherwise.
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
        /// The substituted token provider, which also supports forced refresh.
        /// </summary>
        private readonly IAzureSqlTokenProvider tokenProvider = Substitute.For<IAzureSqlTokenProvider, IAzureSqlTokenRefresher>();

        /// <summary>
        /// The number of refreshes the service has requested so far.
        /// </summary>
        private int refreshCount;

        /// <summary>
        /// Gets the number of refreshes the service has requested so far.
        /// </summary>
        private int RefreshCount => Volatile.Read(ref this.refreshCount);

        /// <summary>
        /// Verifies that the service refreshes once at start and again exactly when the refresh-ahead point is reached.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_RefreshedToken_When_RefreshAheadPointIsReached_Then_RefreshesAgain()
        {
            // Arrange
            this.SetupRefresh(_ => this.timeProvider.GetUtcNow() + TokenLifetime);
            using var service = this.CreateService();
            await service.StartAsync(CancellationToken.None);
            await this.WaitForScheduledRefreshAsync(1);

            // Act
            this.timeProvider.Advance(TokenLifetime - RefreshAheadWindow - TimeSpan.FromSeconds(1));
            await Task.Delay(SettleTime);
            var refreshesBeforeRefreshPoint = this.RefreshCount;
            this.timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => this.RefreshCount >= 2);

            // Assert
            refreshesBeforeRefreshPoint.ShouldBe(1);
            this.RefreshCount.ShouldBe(2);
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
            this.SetupRefresh(_ => this.timeProvider.GetUtcNow() + lifetime);
            using var service = this.CreateService();
            await service.StartAsync(CancellationToken.None);
            await this.WaitForScheduledRefreshAsync(1);

            // Act
            this.timeProvider.Advance(TimeSpan.FromSeconds(59));
            await Task.Delay(SettleTime);
            var refreshesBeforeHalfLifetime = this.RefreshCount;
            this.timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => this.RefreshCount >= 2);

            // Assert
            refreshesBeforeHalfLifetime.ShouldBe(1);
            this.RefreshCount.ShouldBe(2);
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
            this.SetupRefresh(
                _ => throw new InvalidOperationException("refresh failed"),
                _ => this.timeProvider.GetUtcNow() + TokenLifetime);
            using var service = this.CreateService();
            await service.StartAsync(CancellationToken.None);
            await this.WaitForScheduledRefreshAsync(1);

            // Act
            this.timeProvider.Advance(InitialRetryDelay - TimeSpan.FromSeconds(1));
            await Task.Delay(SettleTime);
            var refreshesBeforeRetryPoint = this.RefreshCount;
            this.timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => this.RefreshCount >= 2);

            // Assert
            refreshesBeforeRetryPoint.ShouldBe(1);
            this.RefreshCount.ShouldBe(2);
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
            this.SetupRefresh(_ => this.timeProvider.GetUtcNow() + TokenLifetime);
            using var service = this.CreateService();
            await service.StartAsync(CancellationToken.None);
            await this.WaitForScheduledRefreshAsync(1);

            // Act
            await service.StopAsync(CancellationToken.None);

            // Assert
            service.ExecuteTask.ShouldNotBeNull().Status.ShouldBe(TaskStatus.RanToCompletion);
            this.RefreshCount.ShouldBe(1);
        }

        /// <summary>
        /// Verifies that the service does nothing when background refresh is disabled.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_BackgroundRefreshDisabled_When_Started_Then_DoesNotRefresh()
        {
            // Arrange
            this.SetupRefresh(_ => this.timeProvider.GetUtcNow() + TokenLifetime);
            using var service = this.CreateService(enableBackgroundRefresh: false);

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask.ShouldNotBeNull().WaitAsync(ReactionTimeout);

            // Assert
            service.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
            this.RefreshCount.ShouldBe(0);
        }

        /// <summary>
        /// Verifies that a token provider without forced-refresh support leaves the service idle instead of failing.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_TokenProviderWithoutRefreshSupport_When_Started_Then_CompletesWithoutError()
        {
            // Arrange
            var plainProvider = Substitute.For<IAzureSqlTokenProvider>();
            using var service = this.CreateService(plainProvider);

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask.ShouldNotBeNull().WaitAsync(ReactionTimeout);

            // Assert
            service.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
            await plainProvider.DidNotReceiveWithAnyArgs().GetAzureSqlAccessTokenAsync(CancellationToken.None);
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
        /// Configures the refresher to produce the given results in order, repeating the last one, and counts every request.
        /// </summary>
        /// <param name="results">The results to produce; a result may throw to simulate a failed refresh.</param>
        private void SetupRefresh(params Func<CallInfo, DateTimeOffset>[] results)
        {
            var counted = results
                .Select(result => new Func<CallInfo, DateTimeOffset>(call =>
                {
                    Interlocked.Increment(ref this.refreshCount);
                    return result(call);
                }))
                .ToArray();

            ((IAzureSqlTokenRefresher)this.tokenProvider).RefreshAsync(Arg.Any<CancellationToken>())
                .Returns(counted[0], counted.Skip(1).ToArray());
        }

        /// <summary>
        /// Waits until the service has scheduled its next refresh (or retry) on the fake clock for the given time,
        /// so that advancing the clock afterwards is guaranteed to reach that schedule.
        /// </summary>
        /// <param name="scheduledRefreshes">The number of schedules to wait for, counted from the start of the service.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task WaitForScheduledRefreshAsync(int scheduledRefreshes)
        {
            await WaitUntilAsync(() => this.timeProvider.TimersCreated >= scheduledRefreshes);
            this.timeProvider.TimersCreated.ShouldBe(scheduledRefreshes);
        }

        /// <summary>
        /// Creates the service under test.
        /// </summary>
        /// <param name="provider">The token provider to hand the service, or <see langword="null"/> for the refreshable substitute.</param>
        /// <param name="enableBackgroundRefresh">Whether background refresh is enabled in the options.</param>
        /// <returns>The service.</returns>
        private AzureSqlTokenRefreshService CreateService(IAzureSqlTokenProvider? provider = null, bool enableBackgroundRefresh = true)
        {
            var options = Options.Create(new AzureSqlFederatedIdentityOptions
            {
                RefreshAheadWindow = RefreshAheadWindow,
                EnableBackgroundRefresh = enableBackgroundRefresh,
            });

            return new AzureSqlTokenRefreshService(provider ?? this.tokenProvider, options, this.timeProvider, NullLogger<AzureSqlTokenRefreshService>.Instance);
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
