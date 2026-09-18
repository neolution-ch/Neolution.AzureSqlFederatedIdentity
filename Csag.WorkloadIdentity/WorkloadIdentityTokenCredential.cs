namespace Csag.WorkloadIdentity
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Abstractions;

    /// <summary>
    /// Presents a token provider to Azure SDK clients as a <see cref="TokenCredential"/>, so that for example a
    /// <c>BlobServiceClient</c> can be constructed from an <see cref="IBlobStorageTokenProvider"/>. Every token
    /// request is served by the provider, which reuses its held token and reports that token's real expiry. The
    /// scopes in the request are ignored, because the provider is bound to one resource.
    /// </summary>
    public sealed class WorkloadIdentityTokenCredential : TokenCredential
    {
        /// <summary>
        /// The provider that serves the token requests.
        /// </summary>
        private readonly IAccessTokenProvider tokenProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkloadIdentityTokenCredential"/> class.
        /// </summary>
        /// <param name="tokenProvider">The provider that serves the token requests.</param>
        public WorkloadIdentityTokenCredential(IAccessTokenProvider tokenProvider)
        {
            ArgumentNullException.ThrowIfNull(tokenProvider);

            this.tokenProvider = tokenProvider;
        }

        /// <summary>
        /// Gets the provider's current token, blocking the calling thread while the provider obtains one. Azure SDK
        /// clients call this overload from their synchronous APIs; prefer their asynchronous APIs.
        /// </summary>
        /// <param name="requestContext">The token request, whose scopes are ignored.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token.</returns>
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            // TokenCredential.GetToken is synchronous by contract, so the provider's asynchronous call is waited on
            // here. The library's providers never capture a synchronization context, so the wait cannot deadlock them.
#pragma warning disable S4462 // Calls to "async" methods should not be blocking
            return this.tokenProvider.GetAccessTokenAsync(cancellationToken).GetAwaiter().GetResult();
#pragma warning restore S4462
        }

        /// <summary>
        /// Gets the provider's current token.
        /// </summary>
        /// <param name="requestContext">The token request, whose scopes are ignored.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token.</returns>
        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return await this.tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
