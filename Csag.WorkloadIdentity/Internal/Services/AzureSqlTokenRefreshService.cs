namespace Csag.WorkloadIdentity.Internal.Services
{
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Background service that keeps the Azure SQL access token refreshed ahead of its expiry, so that callers are
    /// served from the held token instead of waiting for a token exchange.
    /// </summary>
    internal class AzureSqlTokenRefreshService : BackgroundService
    {
        /// <summary>
        /// The shortest wait between two refreshes, so that a token which arrives already inside its refresh-ahead
        /// window does not cause a refresh loop.
        /// </summary>
        private static readonly TimeSpan MinimumRefreshDelay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The longest wait between two refreshes. Access tokens live for hours, so this only bounds the wait for a
        /// token with an implausible expiry; the runtime rejects waits beyond roughly 49 days.
        /// </summary>
        private static readonly TimeSpan MaximumRefreshDelay = TimeSpan.FromDays(1);

        /// <summary>
        /// The wait before the first retry after a failed refresh; every further consecutive failure doubles it.
        /// </summary>
        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The longest wait between retries after consecutive failed refreshes.
        /// </summary>
        private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Provides Azure SQL access tokens.
        /// </summary>
        private readonly IAzureSqlTokenProvider tokenProvider;

        /// <summary>
        /// The options for federated identity configuration.
        /// </summary>
        private readonly AzureSqlFederatedIdentityOptions options;

        /// <summary>
        /// The clock that schedules the refreshes.
        /// </summary>
        private readonly TimeProvider timeProvider;

        /// <summary>
        /// The logger instance for this service.
        /// </summary>
        private readonly ILogger<AzureSqlTokenRefreshService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSqlTokenRefreshService"/> class.
        /// </summary>
        /// <param name="tokenProvider">The Azure SQL token provider.</param>
        /// <param name="options">The federated identity options.</param>
        /// <param name="timeProvider">The clock that schedules the refreshes.</param>
        /// <param name="logger">The logger instance.</param>
        public AzureSqlTokenRefreshService(
            IAzureSqlTokenProvider tokenProvider,
            IOptions<AzureSqlFederatedIdentityOptions> options,
            TimeProvider timeProvider,
            ILogger<AzureSqlTokenRefreshService> logger)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.tokenProvider = tokenProvider;
            this.options = options.Value;
            this.timeProvider = timeProvider;
            this.logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!this.options.EnableBackgroundRefresh)
            {
                this.logger.LogDebug("Background refresh of the Azure SQL access token is disabled.");
                return;
            }

            if (this.tokenProvider is not IAzureSqlTokenRefresher refresher)
            {
                this.logger.LogInformation("The registered token provider {TokenProviderType} cannot be refreshed on demand; the Azure SQL access token will not be refreshed in the background.", this.tokenProvider.GetType());
                return;
            }

            this.logger.LogDebug("Background refresh of the Azure SQL access token started; refreshing {RefreshAheadWindow} ahead of expiry.", this.options.RefreshAheadWindow);

            var consecutiveFailures = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan delay;
                try
                {
                    var expiresOn = await refresher.RefreshAsync(stoppingToken).ConfigureAwait(false);
                    consecutiveFailures = 0;
                    delay = this.GetDelayUntilRefresh(expiresOn);
                    this.logger.LogDebug("Azure SQL access token refreshed; it expires at {ExpiresOn} and is refreshed again in {Delay}.", expiresOn, delay);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveFailures++;
                    delay = GetRetryDelay(consecutiveFailures);
                    this.logger.LogError(ex, "Refreshing the Azure SQL access token failed ({ConsecutiveFailures} consecutive failures); retrying in {Delay}.", consecutiveFailures, delay);
                }

                await this.WaitAsync(delay, stoppingToken).ConfigureAwait(false);
            }

            this.logger.LogDebug("Background refresh of the Azure SQL access token is stopping.");
        }

        /// <summary>
        /// Computes the wait before retrying after the given number of consecutive failed refreshes.
        /// </summary>
        /// <param name="consecutiveFailures">The number of consecutive failures so far, at least one.</param>
        /// <returns>The wait, doubling per failure up to <see cref="MaximumRetryDelay"/>.</returns>
        private static TimeSpan GetRetryDelay(int consecutiveFailures)
        {
            var delay = InitialRetryDelay;
            for (var failure = 1; failure < consecutiveFailures && delay < MaximumRetryDelay; failure++)
            {
                delay *= 2;
            }

            return delay < MaximumRetryDelay ? delay : MaximumRetryDelay;
        }

        /// <summary>
        /// Computes the wait until a token expiring at <paramref name="expiresOn"/> should be refreshed.
        /// </summary>
        /// <param name="expiresOn">The instant the held token expires.</param>
        /// <returns>The wait.</returns>
        private TimeSpan GetDelayUntilRefresh(DateTimeOffset expiresOn)
        {
            var remaining = expiresOn - this.timeProvider.GetUtcNow();

            // A window longer than half the remaining lifetime would schedule the refresh almost immediately; a
            // short-lived token is instead used for half its lifetime before being replaced.
            var window = TimeSpan.FromTicks(Math.Min(this.options.RefreshAheadWindow.Ticks, remaining.Ticks / 2));
            var delay = remaining - window;
            return TimeSpan.FromTicks(Math.Clamp(delay.Ticks, MinimumRefreshDelay.Ticks, MaximumRefreshDelay.Ticks));
        }

        /// <summary>
        /// Waits for <paramref name="delay"/>, returning early and without error when the service is stopping.
        /// </summary>
        /// <param name="delay">The wait.</param>
        /// <param name="stoppingToken">The token that signals the service is stopping.</param>
        /// <returns>A task that completes after the wait or once the service is stopping.</returns>
        private async Task WaitAsync(TimeSpan delay, CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(delay, this.timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown ends the wait; the loop observes the cancelled token and exits.
            }
        }
    }
}
