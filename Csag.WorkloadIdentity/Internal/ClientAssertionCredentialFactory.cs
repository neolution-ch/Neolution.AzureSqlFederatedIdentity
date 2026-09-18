namespace Csag.WorkloadIdentity.Internal
{
    using Azure.Core;
    using Azure.Identity;

    /// <summary>
    /// Creates <see cref="ClientAssertionCredential"/> instances.
    /// </summary>
    internal sealed class ClientAssertionCredentialFactory : IClientAssertionCredentialFactory
    {
        /// <inheritdoc />
        public TokenCredential Create(string tenantId, string clientId, Func<CancellationToken, Task<string>> assertionCallback)
        {
            return new ClientAssertionCredential(tenantId, clientId, assertionCallback);
        }
    }
}
