namespace Csag.WorkloadIdentity
{
    /// <summary>
    /// The Azure resources the library obtains access tokens for. Each member carries the OAuth 2.0 scope that is
    /// requested from Microsoft Entra ID for the resource and is named after the
    /// <see cref="Options.WorkloadIdentityOptions"/> property that configures the resource.
    /// </summary>
    public enum TokenScope
    {
        /// <summary>
        /// Azure SQL Database.
        /// </summary>
        [TokenScopeMetadata("https://database.windows.net/.default")]
        AzureSql = 0,

        /// <summary>
        /// Azure Blob Storage.
        /// </summary>
        [TokenScopeMetadata("https://storage.azure.com/.default")]
        BlobStorage = 1,
    }
}
