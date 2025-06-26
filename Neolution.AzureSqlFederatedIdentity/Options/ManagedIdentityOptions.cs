namespace Neolution.AzureSqlFederatedIdentity.Options
{
    /// <summary>
    /// Options for Azure Managed Identity authentication.
    /// Set <see cref="UseSystemAssignedIdentity"/> to true to use the system-assigned identity;
    /// set to false and provide a specific client ID to use a user-assigned identity.
    /// </summary>
    public class ManagedIdentityOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use the system-assigned Managed Identity.
        /// If true, the system-assigned managed identity is used and <see cref="ClientId"/> is ignored.
        /// If false, <see cref="ClientId"/> must be provided for a user-assigned managed identity.
        /// </summary>
        public bool UseSystemAssignedIdentity { get; set; }

        /// <summary>
        /// Gets or sets the client ID of a user-assigned Managed Identity.
        /// Required if <see cref="UseSystemAssignedIdentity"/> is false. Ignored if <see cref="UseSystemAssignedIdentity"/> is true.
        /// </summary>
        public string? ClientId { get; set; }
    }
}
