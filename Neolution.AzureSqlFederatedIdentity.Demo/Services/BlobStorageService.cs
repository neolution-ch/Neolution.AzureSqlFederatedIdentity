namespace Neolution.AzureSqlFederatedIdentity.Demo.Services
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Default implementation of <see cref="IBlobStorageService"/>, downloads a fixed test file.
    /// </summary>
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient blobServiceClient;
        private readonly string containerName;
        private readonly string blobName;

        public BlobStorageService(BlobServiceClient blobServiceClient, IConfiguration configuration)
        {
            this.blobServiceClient = blobServiceClient;
            var path = configuration["BlobStorageTestFile:FilePath"] ?? throw new ArgumentException("BlobStorageTestFile:FilePath is not configured");
            var parts = path.Split('/', 2);
            containerName = parts[0];
            blobName = parts[1];
        }

        public async Task<string> DownloadTestFileAsync(CancellationToken cancellationToken)
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);

            using var reader = new StreamReader(download.Value.Content);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}
