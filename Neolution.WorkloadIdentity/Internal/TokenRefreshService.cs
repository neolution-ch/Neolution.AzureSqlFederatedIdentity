namespace Neolution.WorkloadIdentity.Internal
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Neolution.WorkloadIdentity.Abstractions;

    /// <summary>
    ///     Background service that proactively refreshes and preloads Azure SQL and Blob Storage access tokens.
    /// </summary>
    internal class TokenRefreshService : BackgroundService
    {
        /// <summary>
        ///     Provides methods to retrieve Azure SQL access tokens.
        /// </summary>
        private readonly IAzureSqlTokenProvider azureSqlTokenProvider;

        /// <summary>
        ///     Provides methods to retrieve Blob Storage access tokens.
        /// </summary>
        private readonly IBlobStorageTokenProvider blobStorageTokenProvider;

        /// <summary>
        ///     Provides logging functionality for the <see cref="TokenRefreshService" /> class.
        /// </summary>
        private readonly ILogger<TokenRefreshService> logger;

        /// <summary>
        ///     The interval at which tokens are refreshed and preloaded.
        /// </summary>
        private readonly TimeSpan refreshInterval = TimeSpan.FromMinutes(10);

        /// <summary>
        ///     Initializes a new instance of the <see cref="TokenRefreshService" /> class.
        /// </summary>
        /// <param name="azureSqlTokenProvider">The Azure SQL token provider.</param>
        /// <param name="blobStorageTokenProvider">The Blob Storage token provider.</param>
        /// <param name="logger">The logger instance.</param>
        public TokenRefreshService(
            IAzureSqlTokenProvider azureSqlTokenProvider,
            IBlobStorageTokenProvider blobStorageTokenProvider,
            ILogger<TokenRefreshService> logger)
        {
            this.azureSqlTokenProvider = azureSqlTokenProvider;
            this.blobStorageTokenProvider = blobStorageTokenProvider;
            this.logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.logger.LogDebug("TokenRefreshService started. Will proactively refresh and preload tokens every {Interval}.", this.refreshInterval);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    this.logger.LogDebug("Proactively preloading Azure SQL access token into cache...");
                    var sqlToken = await this.azureSqlTokenProvider.GetAzureSqlAccessTokenAsync(stoppingToken).ConfigureAwait(false);
                    this.logger.LogDebug("Azure SQL token preloading complete. Token length: {Length}", sqlToken?.Length);

                    this.logger.LogDebug("Proactively preloading Blob Storage access token into cache...");
                    var blobToken = await this.blobStorageTokenProvider.GetBlobStorageAccessTokenAsync(stoppingToken).ConfigureAwait(false);
                    this.logger.LogDebug("Blob Storage token preloading complete. Token length: {Length}", blobToken?.Length);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error occurred while preloading tokens.");
                }

                await Task.Delay(this.refreshInterval, stoppingToken).ConfigureAwait(false);
            }

            this.logger.LogDebug("TokenRefreshService is stopping.");
        }
    }
}
