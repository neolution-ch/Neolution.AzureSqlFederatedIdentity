namespace Csag.WorkloadIdentity.Internal.Services
{
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Background service that keeps the access token of every configured resource refreshed ahead of its expiry, so
    /// that callers are served from the held token instead of waiting for a token request. Each resource is
    /// refreshed by its own loop, so a slow or failing resource does not delay the others.
    /// </summary>
    internal sealed class TokenRefreshService : BackgroundService
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
        /// Resolves the token provider of each configured resource once the host starts.
        /// </summary>
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// The options naming the configured resources.
        /// </summary>
        private readonly WorkloadIdentityOptions options;

        /// <summary>
        /// The clock that schedules the refreshes.
        /// </summary>
        private readonly TimeProvider timeProvider;

        /// <summary>
        /// The logger instance for this service.
        /// </summary>
        private readonly ILogger<TokenRefreshService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenRefreshService"/> class.
        /// </summary>
        /// <param name="serviceProvider">Resolves the token provider of each configured resource.</param>
        /// <param name="options">The workload identity options.</param>
        /// <param name="timeProvider">The clock that schedules the refreshes.</param>
        /// <param name="logger">The logger instance.</param>
        public TokenRefreshService(
            IServiceProvider serviceProvider,
            IOptions<WorkloadIdentityOptions> options,
            TimeProvider timeProvider,
            ILogger<TokenRefreshService> logger)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.serviceProvider = serviceProvider;
            this.options = options.Value;
            this.timeProvider = timeProvider;
            this.logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!this.options.EnableBackgroundRefresh)
            {
                this.logger.LogDebug("Background refresh of access tokens is disabled.");
                return;
            }

            var loops = new List<Task>();
            foreach (var scope in Enum.GetValues<TokenScope>())
            {
                if (this.options.GetResource(scope) is null)
                {
                    continue;
                }

                var tokenProvider = this.ResolveTokenProvider(scope);
                if (tokenProvider is not ITokenRefresher refresher)
                {
                    this.logger.LogInformation("The registered token provider {TokenProviderType} for {Scope} cannot be refreshed on demand; its access token will not be refreshed in the background.", tokenProvider.GetType(), scope);
                    continue;
                }

                loops.Add(this.RefreshLoopAsync(scope, refresher, stoppingToken));
            }

            await Task.WhenAll(loops).ConfigureAwait(false);
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
        /// Resolves the registered token provider of the resource.
        /// </summary>
        /// <param name="scope">The resource.</param>
        /// <returns>The token provider.</returns>
        private IAccessTokenProvider ResolveTokenProvider(TokenScope scope)
        {
            return scope switch
            {
                TokenScope.AzureSql => this.serviceProvider.GetRequiredService<IAzureSqlTokenProvider>(),
                TokenScope.BlobStorage => this.serviceProvider.GetRequiredService<IBlobStorageTokenProvider>(),
                _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown token scope."),
            };
        }

        /// <summary>
        /// Refreshes the token of one resource ahead of its expiry until the service stops, retrying failed refreshes
        /// with exponential backoff.
        /// </summary>
        /// <param name="scope">The resource.</param>
        /// <param name="refresher">Refreshes the resource's token.</param>
        /// <param name="stoppingToken">The token that signals the service is stopping.</param>
        /// <returns>A task that completes once the service is stopping.</returns>
        private async Task RefreshLoopAsync(TokenScope scope, ITokenRefresher refresher, CancellationToken stoppingToken)
        {
            this.logger.LogDebug("Background refresh of the {Scope} access token started; refreshing {RefreshAheadWindow} ahead of expiry.", scope, this.options.RefreshAheadWindow);

            var consecutiveFailures = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan delay;
                try
                {
                    var expiresOn = await refresher.RefreshAsync(stoppingToken).ConfigureAwait(false);
                    consecutiveFailures = 0;
                    delay = this.GetDelayUntilRefresh(expiresOn);
                    this.logger.LogDebug("{Scope} access token refreshed; it expires at {ExpiresOn} and is refreshed again in {Delay}.", scope, expiresOn, delay);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveFailures++;
                    delay = GetRetryDelay(consecutiveFailures);
                    this.logger.LogError(ex, "Refreshing the {Scope} access token failed ({ConsecutiveFailures} consecutive failures); retrying in {Delay}.", scope, consecutiveFailures, delay);
                }

                await this.WaitAsync(delay, stoppingToken).ConfigureAwait(false);
            }

            this.logger.LogDebug("Background refresh of the {Scope} access token is stopping.", scope);
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
