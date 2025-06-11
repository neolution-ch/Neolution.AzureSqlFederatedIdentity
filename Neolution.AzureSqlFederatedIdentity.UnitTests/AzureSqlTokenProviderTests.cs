namespace Neolution.AzureSqlFederatedIdentity.UnitTests
{
    using AutoFixture;
    using AutoFixture.AutoNSubstitute;
    using Azure.Core;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Options;
    using NSubstitute;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="AzureSqlTokenProvider"/> class.
    /// </summary>
    public class AzureSqlTokenProviderTests
    {
        /// <summary>
        /// Provides a fixture for creating test objects.
        /// </summary>
        private readonly IFixture fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSqlTokenProviderTests"/> class.
        /// </summary>
        public AzureSqlTokenProviderTests()
        {
            this.fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
        }

        /// <summary>
        /// Verifies that a cached token is returned when available.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_ValidCache_When_GetAzureSqlAccessTokenAsync_Then_ReturnsCachedToken()
        {
            // Arrange
            var token = this.fixture.Create<string>();
            var accessToken = new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(10));
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            const string clientId = "client";
            var options = Options.Create(new AzureSqlFederatedIdentityOptions { ClientId = clientId, TenantId = "tenant" });
            var logger = this.fixture.Create<ILogger<AzureSqlTokenProvider>>();
            var exchanger = this.fixture.Create<IAzureSqlTokenExchanger>();
            memoryCache.Set($"AzureSqlFederatedIdentity_AccessToken_{clientId}", accessToken);
            var provider = new AzureSqlTokenProvider(exchanger, options, logger, memoryCache);

            // Act
            var result = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(token);
            await exchanger.DidNotReceiveWithAnyArgs().ExchangeClientAssertionForAzureTokenAsync(null!, null!, CancellationToken.None);
        }

        /// <summary>
        /// Verifies that a token is fetched and cached when no cached token is available.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_NoCache_When_GetAzureSqlAccessTokenAsync_Then_FetchesAndCachesToken()
        {
            // Arrange
            var token = this.fixture.Create<string>();
            var accessToken = new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(10));
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var options = Options.Create(new AzureSqlFederatedIdentityOptions { ClientId = "client", TenantId = "tenant" });
            var logger = this.fixture.Create<ILogger<AzureSqlTokenProvider>>();
            var exchanger = this.fixture.Create<IAzureSqlTokenExchanger>();
            exchanger.ExchangeClientAssertionForAzureTokenAsync("tenant", "client", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(accessToken));
            var provider = new AzureSqlTokenProvider(exchanger, options, logger, memoryCache);

            // Act
            var result = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(token);
            memoryCache.TryGetValue($"AzureSqlFederatedIdentity_AccessToken_client", out AccessToken cached).ShouldBeTrue();
            cached.Token.ShouldBe(token);
        }
    }
}
