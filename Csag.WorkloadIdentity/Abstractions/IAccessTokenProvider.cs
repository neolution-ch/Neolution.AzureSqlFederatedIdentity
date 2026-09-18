namespace Csag.WorkloadIdentity.Abstractions
{
    using Azure.Core;

    /// <summary>
    /// Provides Microsoft Entra ID access tokens for one Azure resource. Every resource-specific token provider
    /// implements it, so that <see cref="WorkloadIdentityTokenCredential"/> can wrap any of them.
    /// </summary>
    public interface IAccessTokenProvider
    {
        /// <summary>
        /// Gets a valid access token for the resource together with the instant it expires.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token.</returns>
        Task<AccessToken> GetAccessTokenAsync(CancellationToken cancellationToken);
    }
}
