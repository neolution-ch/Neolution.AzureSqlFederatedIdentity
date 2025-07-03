namespace Neolution.AzureSqlFederatedIdentity.Internal
{
    /// <summary>
    /// Represents the Azure OAuth2 token scope for which a token is requested.
    /// </summary>
    public enum AzureTokenScope
    {
        /// <summary>
        /// Represents the token scope for Azure SQL.
        /// </summary>
        [TokenScopeMetadata(nameof(AzureSql), "https://database.windows.net/.default")]
        AzureSql = 0,

        /// <summary>
        /// Represents the token scope for Blob Storage.
        /// </summary>
        [TokenScopeMetadata(nameof(BlobStorage), "https://storage.azure.com/.default")]
        BlobStorage = 1,
    }
}
