namespace Csag.AzureSqlFederatedIdentity
{
    using Csag.AzureSqlFederatedIdentity.Abstractions;
    using Csag.AzureSqlFederatedIdentity.Internal;
    using Csag.AzureSqlFederatedIdentity.Options;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Provides Azure SQL access tokens. The current token is held in memory and reused until it enters the
    /// configured refresh-ahead window; callers that find no usable token share a single token exchange.
    /// </summary>
    public sealed class AzureSqlTokenProvider : IAzureSqlTokenProvider, IAzureSqlTokenRefresher, IDisposable
    {
        /// <summary>
        /// The token exchanger for Azure SQL.
        /// </summary>
        private readonly IAzureSqlTokenExchanger azureSqlTokenExchanger;

        /// <summary>
        /// The options for federated identity configuration.
        /// </summary>
        private readonly AzureSqlFederatedIdentityOptions options;

        /// <summary>
        /// The clock used to judge how much lifetime the held token has left.
        /// </summary>
        private readonly TimeProvider timeProvider;

        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<AzureSqlTokenProvider> logger;

        /// <summary>
        /// Serialises token exchanges: the first caller to find no usable token performs the exchange while the
        /// others wait for it and then reuse its result.
        /// </summary>
        private readonly SemaphoreSlim exchangeLock = new(1, 1);

        /// <summary>
        /// The token currently held, or <see langword="null"/> before the first successful exchange. A reference
        /// type so that the lock-free read in <see cref="GetAzureSqlAccessTokenAsync"/> is atomic.
        /// </summary>
        private volatile HeldToken? currentToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSqlTokenProvider"/> class.
        /// </summary>
        /// <param name="azureSqlTokenExchanger">The token exchanger for Azure SQL.</param>
        /// <param name="options">The federated identity options.</param>
        /// <param name="timeProvider">The clock used to judge token lifetime.</param>
        /// <param name="logger">The logger instance.</param>
        public AzureSqlTokenProvider(
            IAzureSqlTokenExchanger azureSqlTokenExchanger,
            IOptions<AzureSqlFederatedIdentityOptions> options,
            TimeProvider timeProvider,
            ILogger<AzureSqlTokenProvider> logger)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.azureSqlTokenExchanger = azureSqlTokenExchanger;
            this.options = options.Value;
            this.timeProvider = timeProvider;
            this.logger = logger;
        }

        /// <summary>
        /// Gets a valid Azure AD access token for Azure SQL, reusing the held token while it is outside the
        /// refresh-ahead window.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The Azure AD access token for Azure SQL.</returns>
        public async Task<string> GetAzureSqlAccessTokenAsync(CancellationToken cancellationToken)
        {
            var held = this.currentToken;
            if (held is not null && !this.IsDueForRefresh(held))
            {
                this.logger.LogTrace("Returning the held Azure AD access token.");
                return held.Token;
            }

            var token = await this.ExchangeAsync(held, force: false, cancellationToken).ConfigureAwait(false);
            return token.Token;
        }

        /// <inheritdoc />
        async Task<DateTimeOffset> IAzureSqlTokenRefresher.RefreshAsync(CancellationToken cancellationToken)
        {
            var token = await this.ExchangeAsync(this.currentToken, force: true, cancellationToken).ConfigureAwait(false);
            return token.ExpiresOn;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.exchangeLock.Dispose();
        }

        /// <summary>
        /// Exchanges a new token, unless another caller replaced <paramref name="observed"/> with one this caller
        /// can use while it waited for the lock.
        /// </summary>
        /// <param name="observed">The token the caller saw before deciding to exchange, if any.</param>
        /// <param name="force">Whether to replace a token that is not yet due for refresh.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The token now held.</returns>
        private async Task<HeldToken> ExchangeAsync(HeldToken? observed, bool force, CancellationToken cancellationToken)
        {
            await this.exchangeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var held = this.currentToken;
                if (held is not null && this.CanReuse(held, observed, force))
                {
                    this.logger.LogTrace("Another caller refreshed the Azure AD access token in the meantime; reusing it.");
                    return held;
                }

                this.logger.LogDebug("Exchanging a new Azure AD access token for Azure SQL.");
                var accessToken = await this.azureSqlTokenExchanger
                    .ExchangeClientAssertionForAzureTokenAsync(this.options.TenantId, this.options.ClientId, cancellationToken)
                    .ConfigureAwait(false);

                held = new HeldToken(accessToken.Token, accessToken.ExpiresOn);
                this.currentToken = held;
                this.logger.LogDebug("Holding a new Azure AD access token that expires at {ExpiresOn}.", held.ExpiresOn);
                return held;
            }
            finally
            {
                this.exchangeLock.Release();
            }
        }

        /// <summary>
        /// Decides whether the token found under the lock can be returned instead of exchanging a new one.
        /// </summary>
        /// <param name="held">The token currently held.</param>
        /// <param name="observed">The token the caller saw before deciding to exchange, if any.</param>
        /// <param name="force">Whether the caller asked for a forced refresh.</param>
        /// <returns><see langword="true"/> to return <paramref name="held"/>; <see langword="false"/> to exchange.</returns>
        private bool CanReuse(HeldToken held, HeldToken? observed, bool force)
        {
            if (ReferenceEquals(held, observed))
            {
                // Nothing changed while waiting for the lock, so the decision to exchange stands.
                return false;
            }

            // Another caller replaced the token in the meantime. A forced refresh is satisfied by any token that is
            // not yet due. An ordinary call also accepts a short-lived token that arrived already inside the
            // refresh-ahead window, as long as it has not expired, rather than exchanging again straight away.
            return force ? !this.IsDueForRefresh(held) : held.ExpiresOn > this.timeProvider.GetUtcNow();
        }

        /// <summary>
        /// Checks whether the token has entered the refresh-ahead window.
        /// </summary>
        /// <param name="held">The token to check.</param>
        /// <returns><see langword="true"/> if the token is due for refresh.</returns>
        private bool IsDueForRefresh(HeldToken held)
        {
            return held.ExpiresOn - this.timeProvider.GetUtcNow() <= this.options.RefreshAheadWindow;
        }

        /// <summary>
        /// An access token together with the instant it expires.
        /// </summary>
        /// <param name="Token">The access token.</param>
        /// <param name="ExpiresOn">The instant the token expires.</param>
        private sealed record HeldToken(string Token, DateTimeOffset ExpiresOn);
    }
}
