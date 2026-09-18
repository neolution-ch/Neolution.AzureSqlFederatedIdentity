namespace Csag.WorkloadIdentity.Internal
{
    using Azure.Core;
    using Azure.Identity;
    using Csag.WorkloadIdentity.Options;

    /// <summary>
    /// Creates <see cref="ManagedIdentityCredential"/> instances.
    /// </summary>
    internal sealed class ManagedIdentityCredentialFactory : IManagedIdentityCredentialFactory
    {
        /// <inheritdoc />
        public TokenCredential Create(ManagedIdentityOptions managedIdentity)
        {
            ArgumentNullException.ThrowIfNull(managedIdentity);

            if (managedIdentity.UseSystemAssignedIdentity)
            {
                return new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(managedIdentity.ClientId);
            return new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(managedIdentity.ClientId));
        }
    }
}
