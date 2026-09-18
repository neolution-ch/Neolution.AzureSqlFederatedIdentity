namespace Csag.WorkloadIdentity
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Internal;

    /// <summary>
    /// Provides access tokens for Azure Blob Storage. The current token is held in memory and reused until it enters
    /// the configured refresh-ahead window; callers that find no usable token share a single token request.
    /// Registered by <see cref="WorkloadIdentityServiceCollectionExtensions"/> and resolvable only while the
    /// <see cref="Options.WorkloadIdentityOptions.BlobStorage"/> section is configured.
    /// </summary>
    public sealed class BlobStorageTokenProvider : IBlobStorageTokenProvider, ITokenRefresher, IDisposable
    {
        /// <summary>
        /// Holds the token and performs the requests.
        /// </summary>
        private readonly ResourceTokenProvider tokenProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobStorageTokenProvider"/> class.
        /// </summary>
        /// <param name="tokenProvider">The holder of the Blob Storage token.</param>
        internal BlobStorageTokenProvider(ResourceTokenProvider tokenProvider)
        {
            ArgumentNullException.ThrowIfNull(tokenProvider);

            this.tokenProvider = tokenProvider;
        }

        /// <inheritdoc />
        public Task<AccessToken> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            return this.tokenProvider.GetAccessTokenAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<string> GetBlobStorageAccessTokenAsync(CancellationToken cancellationToken)
        {
            var token = await this.tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            return token.Token;
        }

        /// <inheritdoc />
        Task<DateTimeOffset> ITokenRefresher.RefreshAsync(CancellationToken cancellationToken)
        {
            return this.tokenProvider.RefreshAsync(cancellationToken);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.tokenProvider.Dispose();
        }
    }
}
