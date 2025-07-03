namespace Neolution.AzureSqlFederatedIdentity
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Internal;
    using Neolution.AzureSqlFederatedIdentity.Options;

    /// <summary>
    /// Provides Azure AD access tokens for Blob Storage, with caching and refresh.
    /// </summary>
    public class BlobStorageTokenProvider : IBlobStorageTokenProvider
    {
        /// <summary>
        /// The logger instance for logging messages related to the BlobStorageTokenProvider.
        /// </summary>
        private readonly ILogger<BlobStorageTokenProvider> logger;

        /// <summary>
        /// The options for Blob Storage configuration.
        /// </summary>
        private readonly IOptions<BlobStorageOptions> options;

        /// <summary>
        /// The memory cache used to store access tokens.
        /// </summary>
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// The factory for creating workload identity token exchangers.
        /// </summary>
        private readonly WorkloadIdentityTokenExchangerFactory tokenExchangerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobStorageTokenProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="options">The Blob Storage options.</param>
        /// <param name="memoryCache">The memory cache instance.</param>
        /// <param name="tokenExchangerFactory">The token exchanger factory.</param>
        public BlobStorageTokenProvider(
            ILogger<BlobStorageTokenProvider> logger,
            IOptions<BlobStorageOptions> options,
            IMemoryCache memoryCache,
            WorkloadIdentityTokenExchangerFactory tokenExchangerFactory)
        {
            this.logger = logger;
            this.options = options;
            this.memoryCache = memoryCache;
            this.tokenExchangerFactory = tokenExchangerFactory;
        }

        /// <summary>
        /// Gets the cache key for the Blob Storage access token.
        /// </summary>
        public static string TokenCacheKey => $"{nameof(BlobStorageTokenProvider)}_AccessToken";

        /// <summary>
        /// Gets a valid Azure AD access token scoped to Blob Storage.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the access token string.</returns>
        public async Task<string> GetBlobStorageAccessTokenAsync(CancellationToken cancellationToken)
        {
            if (this.memoryCache.TryGetValue<AccessToken>(TokenCacheKey, out var cachedToken) && cachedToken.ExpiresOn.UtcDateTime > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                this.logger.LogTrace("Returning cached Blob Storage access token.");
                return cachedToken.Token;
            }

            this.logger.LogTrace("Fetching new Blob Storage access token.");
            var accessToken = await this.FetchBlobStorageAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            this.SetTokenInCache(accessToken);
            return accessToken.Token;
        }

        /// <summary>
        /// Stores the provided access token in the memory cache with an expiration.
        /// </summary>
        /// <param name="accessToken">The access token to cache.</param>
        private void SetTokenInCache(AccessToken accessToken)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = accessToken.ExpiresOn.UtcDateTime.AddMinutes(-5),
            };
            this.logger.LogTrace("Caching Blob Storage token until {Expiration}.", cacheOptions.AbsoluteExpiration);
            this.memoryCache.Set(TokenCacheKey, accessToken, cacheOptions);
        }

        /// <summary>
        /// Fetches a new Blob Storage access token using the appropriate token exchanger.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the access token.</returns>
        private Task<AccessToken> FetchBlobStorageAccessTokenAsync(CancellationToken cancellationToken)
        {
            var exchanger = this.tokenExchangerFactory.Create(this.options.Value.Provider);
            return exchanger.GetTokenAsync(AzureTokenScope.BlobStorage, cancellationToken);
        }
    }
}
