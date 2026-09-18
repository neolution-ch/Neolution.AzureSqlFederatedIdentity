namespace Csag.WorkloadIdentity.UnitTests
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Internal;
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Time.Testing;
    using NSubstitute;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="AzureSqlTokenProvider"/> class.
    /// </summary>
    public class AzureSqlTokenProviderTests
    {
        /// <summary>
        /// The Azure AD tenant ID the provider under test is configured with.
        /// </summary>
        private const string TenantId = "tenant";

        /// <summary>
        /// The Azure AD client ID the provider under test is configured with.
        /// </summary>
        private const string ClientId = "client";

        /// <summary>
        /// The value of the first token the exchanger hands out.
        /// </summary>
        private const string FirstToken = "first-token";

        /// <summary>
        /// The value of the second token the exchanger hands out.
        /// </summary>
        private const string SecondToken = "second-token";

        /// <summary>
        /// The refresh-ahead window the provider under test is configured with.
        /// </summary>
        private static readonly TimeSpan RefreshAheadWindow = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The instant at which every test starts.
        /// </summary>
        private static readonly DateTimeOffset StartTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// The controllable clock the provider under test reads.
        /// </summary>
        private readonly FakeTimeProvider timeProvider = new(StartTime);

        /// <summary>
        /// The substituted token exchanger.
        /// </summary>
        private readonly IAzureSqlTokenExchanger exchanger = Substitute.For<IAzureSqlTokenExchanger>();

        /// <summary>
        /// Verifies that the first call exchanges a token and returns it.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_NoHeldToken_When_GetAzureSqlAccessTokenAsync_Then_ExchangesAndReturnsToken()
        {
            // Arrange
            this.SetupExchange(new AccessToken(FirstToken, StartTime.AddHours(1)));
            using var provider = this.CreateProvider();

            // Act
            var result = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(FirstToken);
            await this.ShouldHaveExchangedAsync(1);
        }

        /// <summary>
        /// Verifies that a held token outside the refresh-ahead window is returned without another exchange.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_HeldTokenOutsideRefreshWindow_When_GetAzureSqlAccessTokenAsync_Then_ReturnsHeldTokenWithoutExchange()
        {
            // Arrange
            this.SetupExchange(new AccessToken(FirstToken, StartTime.AddHours(1)));
            using var provider = this.CreateProvider();
            await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Act
            var result = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(FirstToken);
            await this.ShouldHaveExchangedAsync(1);
        }

        /// <summary>
        /// Verifies that a token with one tick more than the refresh-ahead window left is still returned.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_HeldTokenOneTickOutsideRefreshWindow_When_GetAzureSqlAccessTokenAsync_Then_ReturnsHeldToken()
        {
            // Arrange
            var expiresOn = StartTime.AddHours(1);
            this.SetupExchange(new AccessToken(FirstToken, expiresOn), new AccessToken(SecondToken, expiresOn.AddHours(1)));
            using var provider = this.CreateProvider();
            await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);
            this.timeProvider.SetUtcNow(expiresOn - RefreshAheadWindow - TimeSpan.FromTicks(1));

            // Act
            var result = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(FirstToken);
            await this.ShouldHaveExchangedAsync(1);
        }

        /// <summary>
        /// Verifies that a token with exactly the refresh-ahead window left is replaced.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_HeldTokenEnteringRefreshWindow_When_GetAzureSqlAccessTokenAsync_Then_ExchangesNewToken()
        {
            // Arrange
            var expiresOn = StartTime.AddHours(1);
            this.SetupExchange(new AccessToken(FirstToken, expiresOn), new AccessToken(SecondToken, expiresOn.AddHours(1)));
            using var provider = this.CreateProvider();
            await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);
            this.timeProvider.SetUtcNow(expiresOn - RefreshAheadWindow);

            // Act
            var result = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(SecondToken);
            await this.ShouldHaveExchangedAsync(2);
        }

        /// <summary>
        /// Verifies that a token which arrives with less than the refresh-ahead window left is used for the call
        /// that fetched it and replaced by the next call, with exactly one exchange per call.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_ExchangedTokenAlreadyInsideRefreshWindow_When_GetAzureSqlAccessTokenAsync_Then_UsesItAndRefreshesOnNextCall()
        {
            // Arrange
            var shortLivedExpiry = StartTime.AddMinutes(2);
            this.SetupExchange(new AccessToken(FirstToken, shortLivedExpiry), new AccessToken(SecondToken, shortLivedExpiry));
            using var provider = this.CreateProvider();

            // Act
            var first = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);
            var second = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            first.ShouldBe(FirstToken);
            second.ShouldBe(SecondToken);
            await this.ShouldHaveExchangedAsync(2);
        }

        /// <summary>
        /// Verifies that concurrent callers on a cold provider share a single exchange.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_FiftyConcurrentCallersAndNoHeldToken_When_GetAzureSqlAccessTokenAsync_Then_ExchangesExactlyOnce()
        {
            // Arrange
            var exchange = new TaskCompletionSource<AccessToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.exchanger.ExchangeClientAssertionForAzureTokenAsync(TenantId, ClientId, Arg.Any<CancellationToken>()).Returns(exchange.Task);
            using var provider = this.CreateProvider();

            // Act
            var calls = Enumerable.Range(0, 50)
                .Select(_ => Task.Run(() => provider.GetAzureSqlAccessTokenAsync(CancellationToken.None)))
                .ToArray();
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            exchange.SetResult(new AccessToken(FirstToken, StartTime.AddHours(1)));
            var results = await Task.WhenAll(calls);

            // Assert
            results.ShouldAllBe(token => token == FirstToken);
            await this.ShouldHaveExchangedAsync(1);
        }

        /// <summary>
        /// Verifies that a failed exchange is not retained and the next call exchanges again.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_FailedExchange_When_GetAzureSqlAccessTokenAsyncAgain_Then_RetriesExchange()
        {
            // Arrange
            this.exchanger.ExchangeClientAssertionForAzureTokenAsync(TenantId, ClientId, Arg.Any<CancellationToken>())
                .Returns(
                    _ => throw new InvalidOperationException("exchange failed"),
                    _ => new AccessToken(FirstToken, StartTime.AddHours(1)));
            using var provider = this.CreateProvider();

            // Act
            var failure = await Should.ThrowAsync<InvalidOperationException>(() => provider.GetAzureSqlAccessTokenAsync(CancellationToken.None));
            var result = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            failure.Message.ShouldBe("exchange failed");
            result.ShouldBe(FirstToken);
            await this.ShouldHaveExchangedAsync(2);
        }

        /// <summary>
        /// Verifies that a forced refresh replaces a token that is not yet due and reports the new expiry.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_HeldTokenOutsideRefreshWindow_When_RefreshAsync_Then_ExchangesNewTokenAndReturnsItsExpiry()
        {
            // Arrange
            var secondExpiry = StartTime.AddHours(2);
            this.SetupExchange(new AccessToken(FirstToken, StartTime.AddHours(1)), new AccessToken(SecondToken, secondExpiry));
            using var provider = this.CreateProvider();
            await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Act
            var expiresOn = await ((IAzureSqlTokenRefresher)provider).RefreshAsync(CancellationToken.None);
            var result = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            expiresOn.ShouldBe(secondExpiry);
            result.ShouldBe(SecondToken);
            await this.ShouldHaveExchangedAsync(2);
        }

        /// <summary>
        /// Verifies that a forced refresh arriving while a caller's exchange is in flight shares that exchange.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_ExchangeInFlight_When_RefreshAsync_Then_SharesThatExchange()
        {
            // Arrange
            var expiresOn = StartTime.AddHours(1);
            var exchange = new TaskCompletionSource<AccessToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.exchanger.ExchangeClientAssertionForAzureTokenAsync(TenantId, ClientId, Arg.Any<CancellationToken>()).Returns(exchange.Task);
            using var provider = this.CreateProvider();

            // Act
            var call = Task.Run(() => provider.GetAzureSqlAccessTokenAsync(CancellationToken.None));
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            var refresh = Task.Run(() => ((IAzureSqlTokenRefresher)provider).RefreshAsync(CancellationToken.None));
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            exchange.SetResult(new AccessToken(FirstToken, expiresOn));

            // Assert
            (await call).ShouldBe(FirstToken);
            (await refresh).ShouldBe(expiresOn);
            await this.ShouldHaveExchangedAsync(1);
        }

        /// <summary>
        /// Configures the exchanger to hand out the given tokens in order, repeating the last one.
        /// </summary>
        /// <param name="tokens">The tokens to hand out.</param>
        private void SetupExchange(params AccessToken[] tokens)
        {
            this.exchanger.ExchangeClientAssertionForAzureTokenAsync(TenantId, ClientId, Arg.Any<CancellationToken>())
                .Returns(tokens[0], tokens.Skip(1).ToArray());
        }

        /// <summary>
        /// Asserts how many times the exchanger was asked for a token.
        /// </summary>
        /// <param name="times">The expected number of exchanges.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task ShouldHaveExchangedAsync(int times)
        {
            await this.exchanger.Received(times).ExchangeClientAssertionForAzureTokenAsync(TenantId, ClientId, Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Creates the provider under test.
        /// </summary>
        /// <returns>The provider.</returns>
        private AzureSqlTokenProvider CreateProvider()
        {
            var options = Options.Create(new AzureSqlFederatedIdentityOptions
            {
                TenantId = TenantId,
                ClientId = ClientId,
                RefreshAheadWindow = RefreshAheadWindow,
            });

            return new AzureSqlTokenProvider(this.exchanger, options, this.timeProvider, NullLogger<AzureSqlTokenProvider>.Instance);
        }
    }
}
