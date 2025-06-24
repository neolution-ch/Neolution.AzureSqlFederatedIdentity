namespace Neolution.AzureSqlFederatedIdentity.Options
{
    /// <summary>
    /// Options for Azure Managed Identity authentication.
    /// Leave <see cref="ClientId"/> null or empty to use the system-assigned identity;
    /// set to a specific client ID to use a user-assigned identity.
    /// </summary>
    public class ManagedIdentityOptions
    {
        /// <summary>
        /// Gets or sets the client ID of a user-assigned Managed Identity.
        /// If null or empty, the system-assigned managed identity is used.
        /// </summary>
        public string? ClientId { get; set; }
    }
}
