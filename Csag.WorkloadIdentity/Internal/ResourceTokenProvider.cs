namespace Csag.WorkloadIdentity.Internal
{
    using Azure.Core;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Holds the access token of one resource. The current token is reused until it enters the refresh-ahead window;
    /// callers that find no usable token share a single token request. Each public resource-specific token provider
    /// wraps one instance.
    /// </summary>
    internal sealed class ResourceTokenProvider : ITokenRefresher, IDisposable
    {
        /// <summary>
        /// The resource whose token is held.
        /// </summary>
        private readonly TokenScope scope;

        /// <summary>
        /// Obtains the tokens.
        /// </summary>
        private readonly IWorkloadIdentityTokenExchanger exchanger;

        /// <summary>
        /// How long before it expires the held token is treated as due for refresh.
        /// </summary>
        private readonly TimeSpan refreshAheadWindow;

        /// <summary>
        /// The clock used to judge how much lifetime the held token has left.
        /// </summary>
        private readonly TimeProvider timeProvider;

        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<ResourceTokenProvider> logger;

        /// <summary>
        /// Serialises token requests: the first caller to find no usable token performs the request while the others
        /// wait for it and then reuse its result.
        /// </summary>
        private readonly SemaphoreSlim exchangeLock = new(1, 1);

        /// <summary>
        /// The token currently held, or <see langword="null"/> before the first successful request. A reference type
        /// so that the lock-free read in <see cref="GetAccessTokenAsync"/> is atomic.
        /// </summary>
        private volatile HeldToken? currentToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceTokenProvider"/> class.
        /// </summary>
        /// <param name="scope">The resource whose token is held.</param>
        /// <param name="exchanger">Obtains the tokens.</param>
        /// <param name="refreshAheadWindow">How long before it expires the held token is treated as due for refresh.</param>
        /// <param name="timeProvider">The clock used to judge token lifetime.</param>
        /// <param name="logger">The logger instance.</param>
        public ResourceTokenProvider(
            TokenScope scope,
            IWorkloadIdentityTokenExchanger exchanger,
            TimeSpan refreshAheadWindow,
            TimeProvider timeProvider,
            ILogger<ResourceTokenProvider> logger)
        {
            this.scope = scope;
            this.exchanger = exchanger;
            this.refreshAheadWindow = refreshAheadWindow;
            this.timeProvider = timeProvider;
            this.logger = logger;
        }

        /// <summary>
        /// Gets a valid access token for the resource, reusing the held token while it is outside the refresh-ahead
        /// window.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token.</returns>
        public async Task<AccessToken> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            var held = this.currentToken;
            if (held is not null && !this.IsDueForRefresh(held))
            {
                this.logger.LogTrace("Returning the held {Scope} access token.", this.scope);
                return held.Value;
            }

            var token = await this.ExchangeAsync(held, force: false, cancellationToken).ConfigureAwait(false);
            return token.Value;
        }

        /// <inheritdoc />
        public async Task<DateTimeOffset> RefreshAsync(CancellationToken cancellationToken)
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
        /// Obtains a new token, unless another caller replaced <paramref name="observed"/> with one this caller can
        /// use while it waited for the lock.
        /// </summary>
        /// <param name="observed">The token the caller saw before deciding to request a new one, if any.</param>
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
                    this.logger.LogTrace("Another caller refreshed the {Scope} access token in the meantime; reusing it.", this.scope);
                    return held;
                }

                this.logger.LogDebug("Obtaining a new {Scope} access token.", this.scope);
                var accessToken = await this.exchanger.ExchangeAsync(this.scope, cancellationToken).ConfigureAwait(false);

                held = new HeldToken(accessToken);
                this.currentToken = held;
                this.logger.LogDebug("Holding a new {Scope} access token that expires at {ExpiresOn}.", this.scope, held.ExpiresOn);
                return held;
            }
            finally
            {
                this.exchangeLock.Release();
            }
        }

        /// <summary>
        /// Decides whether the token found under the lock can be returned instead of requesting a new one.
        /// </summary>
        /// <param name="held">The token currently held.</param>
        /// <param name="observed">The token the caller saw before deciding to request a new one, if any.</param>
        /// <param name="force">Whether the caller asked for a forced refresh.</param>
        /// <returns><see langword="true"/> to return <paramref name="held"/>; <see langword="false"/> to request a new token.</returns>
        private bool CanReuse(HeldToken held, HeldToken? observed, bool force)
        {
            if (ReferenceEquals(held, observed))
            {
                // Nothing changed while waiting for the lock, so the decision to request a new token stands.
                return false;
            }

            // Another caller replaced the token in the meantime. A forced refresh is satisfied by any token that is
            // not yet due. An ordinary call also accepts a short-lived token that arrived already inside the
            // refresh-ahead window, as long as it has not expired, rather than requesting again straight away.
            return force ? !this.IsDueForRefresh(held) : held.ExpiresOn > this.timeProvider.GetUtcNow();
        }

        /// <summary>
        /// Checks whether the token has entered the refresh-ahead window.
        /// </summary>
        /// <param name="held">The token to check.</param>
        /// <returns><see langword="true"/> if the token is due for refresh.</returns>
        private bool IsDueForRefresh(HeldToken held)
        {
            return held.ExpiresOn - this.timeProvider.GetUtcNow() <= this.refreshAheadWindow;
        }

        /// <summary>
        /// An access token held by reference, so that replacing it is atomic.
        /// </summary>
        /// <param name="Value">The access token.</param>
        private sealed record HeldToken(AccessToken Value)
        {
            /// <summary>
            /// Gets the instant the token expires.
            /// </summary>
            public DateTimeOffset ExpiresOn => this.Value.ExpiresOn;
        }
    }
}
