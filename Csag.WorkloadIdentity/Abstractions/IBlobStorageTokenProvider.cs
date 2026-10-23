namespace Csag.WorkloadIdentity.Abstractions
{
    /// <summary>
    /// Provides access tokens for Azure Blob Storage.
    /// </summary>
    public interface IBlobStorageTokenProvider : IAccessTokenProvider
    {
        /// <summary>
        /// Gets a valid access token for Azure Blob Storage.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token as a string.</returns>
        Task<string> GetBlobStorageAccessTokenAsync(CancellationToken cancellationToken);
    }
}
