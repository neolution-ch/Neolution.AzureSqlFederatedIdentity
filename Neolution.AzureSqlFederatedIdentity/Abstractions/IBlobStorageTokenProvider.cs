namespace Neolution.AzureSqlFederatedIdentity.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides Azure AD access tokens for Blob Storage.
    /// </summary>
    public interface IBlobStorageTokenProvider
    {
        /// <summary>
        /// Gets a valid Azure AD access token scoped to Blob Storage.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the Azure AD access token.</returns>
        Task<string> GetBlobStorageAccessTokenAsync(CancellationToken cancellationToken);
    }
}
