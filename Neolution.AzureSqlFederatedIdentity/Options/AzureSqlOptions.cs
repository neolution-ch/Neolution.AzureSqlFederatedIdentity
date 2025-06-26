namespace Neolution.AzureSqlFederatedIdentity.Options
{
    /// <summary>
    /// Root configuration for Azure SQL identity authentication.
    /// </summary>
    public class AzureSqlOptions
    {
        /// <summary>
        /// Gets or sets the provider to use for identity: ManagedIdentity, Google, etc.
        /// </summary>
        public WorkloadIdentityProvider Provider { get; set; } = WorkloadIdentityProvider.ManagedIdentity;

        /// <summary>
        /// Gets or sets options for Managed Identity authentication.
        /// </summary>
        public ManagedIdentityOptions ManagedIdentity { get; set; } = new ManagedIdentityOptions();

        /// <summary>
        /// Gets or sets options for Google federated identity authentication.
        /// Used when Provider = "Google".
        /// </summary>
        public GoogleOptions Google { get; set; } = new GoogleOptions();
    }
}
