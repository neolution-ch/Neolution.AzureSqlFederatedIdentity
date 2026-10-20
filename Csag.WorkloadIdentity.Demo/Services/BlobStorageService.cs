namespace Csag.WorkloadIdentity.Demo.Services
{
    using Azure.Storage.Blobs;

    public class BlobStorageService(BlobClient blobClient) : IBlobStorageService
    {
        public async Task<string> DownloadTestFileAsync(CancellationToken cancellationToken)
        {
            var download = await blobClient.DownloadContentAsync(cancellationToken);
            return download.Value.Content.ToString();
        }
    }
}
