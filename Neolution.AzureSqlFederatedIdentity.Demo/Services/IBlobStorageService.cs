namespace Neolution.AzureSqlFederatedIdentity.Demo.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods to interact with Blob Storage.
    /// </summary>
    public interface IBlobStorageService
    {
        /// <summary>
        /// Downloads the test file content from Blob Storage.
        /// </summary>
        Task<string> DownloadTestFileAsync(CancellationToken cancellationToken);
    }
}
