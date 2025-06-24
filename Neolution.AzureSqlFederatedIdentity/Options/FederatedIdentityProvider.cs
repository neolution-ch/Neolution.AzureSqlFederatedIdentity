namespace Neolution.AzureSqlFederatedIdentity.Options
{
    /// <summary>
    /// Supported identity providers for Azure SQL federated identity.
    /// </summary>
    public enum FederatedIdentityProvider
    {
        /// <summary>
        /// Use Azure Managed Identity (system-assigned or user-assigned).
        /// </summary>
        ManagedIdentity,

        /// <summary>
        /// Use Google Cloud federated identity.
        /// </summary>
        Google,

        // Future providers (AWS, GitHub, etc.) can be added here.
    }
}
