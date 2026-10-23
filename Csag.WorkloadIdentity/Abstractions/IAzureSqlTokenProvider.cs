namespace Csag.WorkloadIdentity.Abstractions
{
    /// <summary>
    /// Provides access tokens for Azure SQL.
    /// </summary>
    public interface IAzureSqlTokenProvider : IAccessTokenProvider
    {
        /// <summary>
        /// Gets a valid access token for Azure SQL, to be assigned to <c>SqlConnection.AccessToken</c>.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token as a string.</returns>
        Task<string> GetAzureSqlAccessTokenAsync(CancellationToken cancellationToken);
    }
}
