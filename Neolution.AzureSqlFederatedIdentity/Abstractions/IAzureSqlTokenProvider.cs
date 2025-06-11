namespace Neolution.AzureSqlFederatedIdentity.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a method to obtain an Azure SQL access token.
    /// </summary>
    public interface IAzureSqlTokenProvider
    {
        /// <summary>
        /// Gets an Azure SQL access token asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The Azure SQL access token as a string.</returns>
        Task<string> GetAzureSqlAccessTokenAsync(CancellationToken cancellationToken);
    }
}
