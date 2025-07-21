namespace Neolution.WorkloadIdentity
{
    /// <summary>
    /// Represents the logical token scope for which a token is requested.
    /// </summary>
    public enum TokenScope
    {
        /// <summary>
        /// Represents the token scope for Azure SQL.
        /// </summary>
        [TokenScopeMetadata("https://database.windows.net/.default", IdentityProvider.Azure)]
        AzureSql = 0,

        /// <summary>
        /// Represents the token scope for Blob Storage.
        /// </summary>
        [TokenScopeMetadata("https://storage.azure.com/.default", IdentityProvider.Azure)]
        BlobStorage = 1,
    }
}
