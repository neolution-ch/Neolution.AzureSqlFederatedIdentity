namespace Neolution.AzureSqlFederatedIdentity.Abstractions
{
    using Azure.Core;

    /// <summary>
    /// Provides a method to exchange a client assertion for an Azure AD access token for Azure SQL.
    /// </summary>
    public interface IAzureSqlTokenExchanger
    {
        /// <summary>
        /// Exchanges a client assertion for an Azure AD access token for Azure SQL.
        /// </summary>
        /// <param name="tenantId">The Azure AD tenant ID.</param>
        /// <param name="clientId">The Azure AD client ID.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An Azure AD access token for Azure SQL.</returns>
        Task<AccessToken> ExchangeClientAssertionForAzureTokenAsync(string tenantId, string clientId, CancellationToken cancellationToken);
    }
}
