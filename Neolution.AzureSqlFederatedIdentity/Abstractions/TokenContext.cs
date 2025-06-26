namespace Neolution.AzureSqlFederatedIdentity.Abstractions
{
    /// <summary>
    /// Represents the logical context for which a token is requested.
    /// </summary>
    public enum TokenContext
    {
        AzureSql,
        BlobStorage,

        // Add more contexts as needed
    }
}
