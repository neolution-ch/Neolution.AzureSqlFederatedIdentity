namespace Neolution.WorkloadIdentity.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using AutoFixture.AutoNSubstitute;
    using Azure.Core;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Neolution.WorkloadIdentity.Internal;
    using Neolution.WorkloadIdentity.Options;
    using NSubstitute;
    using Shouldly;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="BlobStorageTokenProvider"/> class.
    /// </summary>
    public class BlobStorageTokenProviderTests
    {
        /// <summary>
        /// Provides a fixture for creating test objects.
        /// </summary>
        private readonly IFixture fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobStorageTokenProviderTests"/> class.
        /// </summary>
        public BlobStorageTokenProviderTests()
        {
            this.fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
        }

        /// <summary>
        /// Tests that when a valid token is present in the cache, the cached token is returned
        /// without invoking the token exchanger.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_ValidCache_When_GetBlobStorageAccessTokenAsync_Then_ReturnsCachedToken()
        {
            // Arrange
            var token = this.fixture.Create<string>();
            var accessToken = new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(10));
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            memoryCache.Set(BlobStorageTokenProvider.TokenCacheKey, accessToken);
            var logger = this.fixture.Create<ILogger<BlobStorageTokenProvider>>();
            var options = Options.Create(new BlobStorageOptions { Provider = WorkloadIdentityProvider.ManagedIdentity });
            var exchanger = this.fixture.Create<IWorkloadIdentityTokenExchanger>();
            var factory = new WorkloadIdentityTokenExchangerFactory(
                Substitute.For<IServiceProvider>(),
                new Dictionary<WorkloadIdentityProvider, Func<IServiceProvider, IWorkloadIdentityTokenExchanger>>
                {
                    [WorkloadIdentityProvider.ManagedIdentity] = sp => exchanger,
                });
            var provider = new BlobStorageTokenProvider(logger, options, memoryCache, factory);

            // Act
            var result = await provider.GetBlobStorageAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(token);
            await exchanger.DidNotReceive().GetTokenAsync(Arg.Any<TokenScope>(), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Tests that when no token is present in the cache, a new token is fetched using the token exchanger
        /// and cached for future use.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_NoCache_When_GetBlobStorageAccessTokenAsync_Then_FetchesAndCachesToken()
        {
            // Arrange
            var token = this.fixture.Create<string>();
            var accessToken = new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(10));
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var logger = this.fixture.Create<ILogger<BlobStorageTokenProvider>>();
            var options = Options.Create(new BlobStorageOptions { Provider = WorkloadIdentityProvider.ManagedIdentity });
            var exchanger = this.fixture.Create<IWorkloadIdentityTokenExchanger>();
            exchanger.GetTokenAsync(Arg.Any<TokenScope>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(accessToken));
            var factory = new WorkloadIdentityTokenExchangerFactory(
                Substitute.For<IServiceProvider>(),
                new Dictionary<WorkloadIdentityProvider, Func<IServiceProvider, IWorkloadIdentityTokenExchanger>>
                {
                    [WorkloadIdentityProvider.ManagedIdentity] = sp => exchanger,
                });
            var provider = new BlobStorageTokenProvider(logger, options, memoryCache, factory);

            // Act
            var result = await provider.GetBlobStorageAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(token);
            memoryCache.TryGetValue(BlobStorageTokenProvider.TokenCacheKey, out AccessToken cached).ShouldBeTrue();
            cached.Token.ShouldBe(token);
            await exchanger.Received(1).GetTokenAsync(Arg.Any<TokenScope>(), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Tests that when a near-expiry token is present in the cache, the token is refreshed
        /// by invoking the token exchanger.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_NearExpiryCache_When_GetBlobStorageAccessTokenAsync_Then_RefreshesToken()
        {
            // Arrange
            var tokenOld = this.fixture.Create<string>();
            var accessTokenOld = new AccessToken(tokenOld, DateTimeOffset.UtcNow.AddMinutes(5));
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            memoryCache.Set(BlobStorageTokenProvider.TokenCacheKey, accessTokenOld);
            var logger = this.fixture.Create<ILogger<BlobStorageTokenProvider>>();
            var options = Options.Create(new BlobStorageOptions { Provider = WorkloadIdentityProvider.ManagedIdentity });
            var exchanger = this.fixture.Create<IWorkloadIdentityTokenExchanger>();
            var tokenNew = this.fixture.Create<string>();
            var accessTokenNew = new AccessToken(tokenNew, DateTimeOffset.UtcNow.AddMinutes(10));
            exchanger.GetTokenAsync(Arg.Any<TokenScope>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(accessTokenNew));
            var factory = new WorkloadIdentityTokenExchangerFactory(
                Substitute.For<IServiceProvider>(),
                new Dictionary<WorkloadIdentityProvider, Func<IServiceProvider, IWorkloadIdentityTokenExchanger>>
                {
                    [WorkloadIdentityProvider.ManagedIdentity] = sp => exchanger,
                });
            var provider = new BlobStorageTokenProvider(logger, options, memoryCache, factory);

            // Act
            var result = await provider.GetBlobStorageAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(tokenNew);
            memoryCache.TryGetValue(BlobStorageTokenProvider.TokenCacheKey, out AccessToken cached).ShouldBeTrue();
            cached.Token.ShouldBe(tokenNew);
            await exchanger.Received(1).GetTokenAsync(TokenScope.BlobStorage, Arg.Any<CancellationToken>());
        }
    }
}
