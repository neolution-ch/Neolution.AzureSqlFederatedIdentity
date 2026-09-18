namespace Csag.WorkloadIdentity.Demo.Services
{
    using System;
    using Azure.Storage.Blobs;

    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobClient blobClient;

        public BlobStorageService(BlobServiceClient blobServiceClient, string filePath)
        {
            // The test file is addressed as "<container>/<blob>"; the blob name may itself contain slashes.
            var separator = filePath.IndexOf('/');
            if (separator <= 0 || separator == filePath.Length - 1)
            {
                throw new ArgumentException("The test file path must have the form '<container>/<blob>'.", nameof(filePath));
            }

            this.blobClient = blobServiceClient.GetBlobContainerClient(filePath[..separator]).GetBlobClient(filePath[(separator + 1)..]);
        }

        public async Task<string> DownloadTestFileAsync(CancellationToken cancellationToken)
        {
            var download = await this.blobClient.DownloadContentAsync(cancellationToken);
            return download.Value.Content.ToString();
        }
    }
}
