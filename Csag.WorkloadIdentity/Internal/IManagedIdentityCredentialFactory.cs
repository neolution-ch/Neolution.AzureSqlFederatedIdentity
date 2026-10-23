namespace Csag.WorkloadIdentity.Internal
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Options;

    /// <summary>
    /// Creates the Azure credential that requests tokens from the managed identity endpoint. Exists so that the
    /// exchanger can be tested without running on an Azure resource.
    /// </summary>
    internal interface IManagedIdentityCredentialFactory
    {
        /// <summary>
        /// Creates a credential for the managed identity the options describe: the system-assigned identity, or the
        /// user-assigned identity with the given client ID.
        /// </summary>
        /// <param name="managedIdentity">The managed identity.</param>
        /// <returns>The credential.</returns>
        TokenCredential Create(ManagedIdentityOptions managedIdentity);
    }
}
