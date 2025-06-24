namespace Neolution.AzureSqlFederatedIdentity.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;

    /// <summary>
    /// Provides a method to retrieve an Azure AD access token for Azure SQL.
    /// </summary>
    public interface ITokenExchanger
    {
        /// <summary>
        /// Gets an Azure AD access token for Azure SQL.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An Azure AD access token for Azure SQL.</returns>
        Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken);
    }
}
