namespace Neolution.AzureSqlFederatedIdentity
{
    using Azure.Core;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Internal;
    using Neolution.AzureSqlFederatedIdentity.Options;

    /// <summary>
    /// Provides Azure SQL access tokens, with caching and automatic refresh.
    /// </summary>
    public class AzureSqlTokenProvider : IAzureSqlTokenProvider
    {
        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<AzureSqlTokenProvider> logger;

        /// <summary>
        /// The options
        /// </summary>
        private readonly IOptions<AzureSqlOptions> options;

        /// <summary>
        /// The memory cache for storing tokens.
        /// </summary>
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// The token exchanger factory.
        /// </summary>
        private readonly WorkloadIdentityTokenExchangerFactory tokenExchangerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSqlTokenProvider" /> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="options">The options.</param>
        /// <param name="memoryCache">The memory cache instance.</param>
        /// <param name="tokenExchangerFactory">The token exchanger factory.</param>
        /// <exception cref="System.ArgumentNullException">tokenExchanger</exception>
        public AzureSqlTokenProvider(
            ILogger<AzureSqlTokenProvider> logger,
            IOptions<AzureSqlOptions> options,
            IMemoryCache memoryCache,
            WorkloadIdentityTokenExchangerFactory tokenExchangerFactory)
        {
            this.logger = logger;
            this.options = options;
            this.memoryCache = memoryCache;
            this.tokenExchangerFactory = tokenExchangerFactory;
        }

        /// <summary>
        /// Gets the cache key for the Azure SQL access token.
        /// </summary>
        public static string TokenCacheKey => $"{nameof(AzureSqlTokenProvider)}_AccessToken";

        /// <summary>
        /// Gets a valid Azure AD access token for Azure SQL, using the cache if possible.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The Azure AD access token for Azure SQL.</returns>
        public async Task<string> GetAzureSqlAccessTokenAsync(CancellationToken cancellationToken)
        {
            // Returns a valid Azure AD access token for Azure SQL, using the cache if possible.
            if (this.memoryCache.TryGetValue<AccessToken>(TokenCacheKey, out var cachedToken))
            {
                if (cachedToken.ExpiresOn.UtcDateTime > DateTimeOffset.UtcNow.AddMinutes(5))
                {
                    this.logger.LogTrace("Returning cached Azure AD access token from IMemoryCache.");
                    return cachedToken.Token;
                }

                this.logger.LogTrace("Cached Azure AD access token is close to expiring, fetching a new one.");
            }
            else
            {
                this.logger.LogTrace("No cached Azure AD access token found in IMemoryCache, fetching a new one.");
            }

            var accessToken = await this.FetchAzureSqlAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            this.SetTokenInCache(accessToken);
            return accessToken.Token;
        }

        /// <summary>
        /// Sets the access token in the memory cache.
        /// </summary>
        /// <param name="accessToken">The access token to cache.</param>
        private void SetTokenInCache(AccessToken accessToken)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                // Expire the token in cache 5 minutes before its actual expiry to avoid using a token
                // that is close to expiring or already expired, which would cause authentication failures.
                AbsoluteExpiration = accessToken.ExpiresOn.UtcDateTime.AddMinutes(-5),
            };

            this.logger.LogTrace("Caching Azure AD access token in IMemoryCache with expiration at {Expiration}.", cacheEntryOptions.AbsoluteExpiration);
            this.memoryCache.Set(TokenCacheKey, accessToken, cacheEntryOptions);
        }

        /// <summary>
        /// Fetches a new Azure AD access token for Azure SQL from the token exchanger.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The new Azure AD access token.</returns>
        private Task<AccessToken> FetchAzureSqlAccessTokenAsync(CancellationToken cancellationToken)
        {
            var exchanger = this.tokenExchangerFactory.Create(this.options.Value.Provider);
            return exchanger.GetTokenAsync(AzureTokenScope.AzureSql, cancellationToken);
        }
    }
}
