namespace Neolution.AzureSqlFederatedIdentity.Options
{
    /// <summary>
    /// Top-level configuration for all workload identity scenarios.
    /// </summary>
    public class WorkloadIdentityOptions
    {
        /// <summary>
        /// Gets or sets the configuration for Azure SQL workload identity.
        /// </summary>
        public AzureSqlOptions AzureSql { get; set; } = new AzureSqlOptions();

        /// <summary>
        /// Gets or sets the configuration for Azure Blob Storage workload identity.
        /// </summary>
        public BlobStorageOptions BlobStorage { get; set; } = new BlobStorageOptions();
    }
}
