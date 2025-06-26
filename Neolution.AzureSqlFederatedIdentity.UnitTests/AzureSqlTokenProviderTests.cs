namespace Neolution.AzureSqlFederatedIdentity.UnitTests
{
    using AutoFixture;
    using AutoFixture.AutoNSubstitute;
    using Azure.Core;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Internal;
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
            memoryCache.Set(AzureSqlTokenProvider.TokenCacheKey, accessToken);
            var logger = this.fixture.Create<ILogger<AzureSqlTokenProvider>>();
            var options = Microsoft.Extensions.Options.Options.Create(new AzureSqlOptions { Provider = WorkloadIdentityProvider.ManagedIdentity });
            var exchanger = Substitute.For<IWorkloadIdentityTokenExchanger>();
            var factory = Substitute.For<WorkloadIdentityTokenExchangerFactory>(null, null);
            factory.Create(WorkloadIdentityProvider.ManagedIdentity).Returns(exchanger);
            var provider = new AzureSqlTokenProvider(logger, options, memoryCache, factory);

            // Act
            var result = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(token);
            await exchanger.DidNotReceive().GetTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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
            var logger = this.fixture.Create<ILogger<AzureSqlTokenProvider>>();
            var options = Microsoft.Extensions.Options.Options.Create(new AzureSqlOptions { Provider = WorkloadIdentityProvider.ManagedIdentity });
            var exchanger = Substitute.For<IWorkloadIdentityTokenExchanger>();
            exchanger.GetTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(accessToken));
            var factory = Substitute.For<WorkloadIdentityTokenExchangerFactory>(null, null);
            factory.Create(WorkloadIdentityProvider.ManagedIdentity).Returns(exchanger);
            var provider = new AzureSqlTokenProvider(logger, options, memoryCache, factory);

            // Act
            var result = await provider.GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(token);
            memoryCache.TryGetValue(AzureSqlTokenProvider.TokenCacheKey, out AccessToken cached).ShouldBeTrue();
            cached.Token.ShouldBe(token);
            await exchanger.Received(1).GetTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }
}
