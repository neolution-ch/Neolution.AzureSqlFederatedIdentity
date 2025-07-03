namespace Neolution.WorkloadIdentity.Internal
{
    /// <summary>
    /// Represents the types of resources that can be used with workload identity.
    /// </summary>
    public enum WorkloadIdentityResourceType
    {
        /// <summary>
        /// Represents an Azure SQL resource.
        /// </summary>
        AzureSql = 0,

        /// <summary>
        /// Represents a Blob Storage resource.
        /// </summary>
        BlobStorage = 1,
    }
}
