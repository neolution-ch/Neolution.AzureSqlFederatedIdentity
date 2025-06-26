namespace Neolution.AzureSqlFederatedIdentity.Abstractions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides mappings from TokenContext to scope and options name for all providers.
    /// </summary>
    public static class TokenContextMappings
    {
        public static readonly IReadOnlyDictionary<TokenContext, (string Scope, string OptionsName)> Map =
            new Dictionary<TokenContext, (string, string)>
            {
                { TokenContext.AzureSql, ("https://database.windows.net/.default", nameof(TokenContext.AzureSql)) },
                { TokenContext.BlobStorage, ("https://storage.azure.com/.default", nameof(TokenContext.BlobStorage)) },
                // Add more mappings as needed
            };
    }
}
