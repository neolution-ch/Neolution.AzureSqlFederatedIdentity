namespace Neolution.AzureSqlFederatedIdentity.Options
{
    /// <summary>
    /// Configuration for Azure Blob Storage workload identity.
    /// </summary>
    public class BlobStorageOptions
    {
        /// <summary>
        /// Gets or sets the provider to use: ManagedIdentity or Google (or future providers).
        /// </summary>
        public WorkloadIdentityProvider Provider { get; set; } = WorkloadIdentityProvider.ManagedIdentity;

        /// <summary>
        /// Gets or sets options for Managed Identity authentication.
        /// Leave <see cref="ManagedIdentityOptions.ClientId"/> null or empty to use the system-assigned identity;
        /// set to a specific client ID to use a user-assigned identity.
        /// </summary>
        public ManagedIdentityOptions ManagedIdentity { get; set; } = new ManagedIdentityOptions();

        /// <summary>
        /// Gets or sets options for Google federated identity authentication.
        /// Used when Provider = Google.
        /// </summary>
        public GoogleOptions Google { get; set; } = new GoogleOptions();
    }
}
