namespace Neolution.AzureSqlFederatedIdentity.Internal.Services
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;

    /// <summary>
    /// Background service that proactively refreshes and preloads Azure SQL access tokens.
    /// </summary>
    internal class AzureSqlTokenRefreshService : BackgroundService
    {
        /// <summary>
        /// Provides Azure SQL access tokens.
        /// </summary>
        private readonly IAzureSqlTokenProvider tokenProvider;

        /// <summary>
        /// The logger instance for this service.
        /// </summary>
        private readonly ILogger<AzureSqlTokenRefreshService> logger;

        /// <summary>
        /// The interval at which the token is refreshed.
        /// </summary>
        private readonly TimeSpan refreshInterval = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSqlTokenRefreshService"/> class.
        /// </summary>
        /// <param name="tokenProvider">The Azure SQL token provider.</param>
        /// <param name="logger">The logger instance.</param>
        public AzureSqlTokenRefreshService(IAzureSqlTokenProvider tokenProvider, ILogger<AzureSqlTokenRefreshService> logger)
        {
            this.tokenProvider = tokenProvider;
            this.logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.logger.LogDebug("AzureSqlTokenRefreshService started. Will proactively refresh and preload token every {Interval}.", this.refreshInterval);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    this.logger.LogDebug("Proactively preloading Azure AD access token into cache...");
                    var token = await this.tokenProvider.GetAzureSqlAccessTokenAsync(stoppingToken).ConfigureAwait(false);
                    this.logger.LogDebug("Token preloading complete. Token length: {Length}", token?.Length);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error occurred while preloading Azure AD access token.");
                }

                await Task.Delay(this.refreshInterval, stoppingToken).ConfigureAwait(false);
            }

            this.logger.LogDebug("AzureSqlTokenRefreshService is stopping.");
        }
    }
}
