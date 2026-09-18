namespace Csag.WorkloadIdentity.Internal
{
    using Azure.Core;

    /// <summary>
    /// Creates the Azure credential that exchanges a client assertion for an Azure AD access token. Exists so that
    /// the exchanger can be tested without a live Azure AD tenant.
    /// </summary>
    internal interface IClientAssertionCredentialFactory
    {
        /// <summary>
        /// Creates a credential for the given tenant and client that obtains its client assertion from
        /// <paramref name="assertionCallback"/>.
        /// </summary>
        /// <param name="tenantId">The Azure AD tenant ID.</param>
        /// <param name="clientId">The Azure AD client ID.</param>
        /// <param name="assertionCallback">Produces the signed client assertion. The credential passes it the cancellation token of the token request it is serving.</param>
        /// <returns>The credential.</returns>
        TokenCredential Create(string tenantId, string clientId, Func<CancellationToken, Task<string>> assertionCallback);
    }
}
