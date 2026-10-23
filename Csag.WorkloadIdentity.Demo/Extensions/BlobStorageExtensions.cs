namespace Csag.WorkloadIdentity.Demo.Extensions
{
    using System;
    using Azure.Storage.Blobs;
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Demo.Services;
    using Csag.WorkloadIdentity.Options;

    public static class BlobStorageExtensions
    {
        public static IServiceCollection AddBlobStorageClient(this IServiceCollection services, IConfiguration configuration)
        {
            // Blob Storage is optional in the Demo. The client is only registered when the library has a BlobStorage
            // resource to obtain tokens for and a test file is named, so that a run configured for Azure SQL alone
            // still starts and /test reports the blob part as skipped.
            var resourceSection = configuration.GetSection($"{WorkloadIdentityOptions.ConfigurationSectionName}:{nameof(WorkloadIdentityOptions.BlobStorage)}");
            var endpoint = configuration["BlobStorageTestFile:Endpoint"];
            var filePath = configuration["BlobStorageTestFile:FilePath"];
            if (!resourceSection.Exists() || string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(filePath))
            {
                return services;
            }

            // A malformed value stops the app at startup, not each /test. On Linux, Uri.TryCreate accepts a bare path
            // as a file URI; the scheme check rejects it. The blob name in "<container>/<blob>" can contain slashes.
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) || endpointUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"BlobStorageTestFile:Endpoint must be an absolute https URI such as 'https://<account>.blob.core.windows.net', but is '{endpoint}'.");
            }

            var separator = filePath.IndexOf('/', StringComparison.Ordinal);
            if (separator <= 0 || separator == filePath.Length - 1)
            {
                throw new InvalidOperationException($"BlobStorageTestFile:FilePath must have the form '<container>/<blob>', but is '{filePath}'.");
            }

            var containerName = filePath[..separator];
            var blobName = filePath[(separator + 1)..];

            // The library's TokenCredential adapter serves the client from the held Blob Storage token and reports
            // that token's real expiry, so the Azure SDK asks for a new one only when the provider refreshes it.
            services.AddSingleton(serviceProvider => new BlobServiceClient(
                endpointUri,
                new WorkloadIdentityTokenCredential(serviceProvider.GetRequiredService<IBlobStorageTokenProvider>())));
            services.AddSingleton<IBlobStorageService>(serviceProvider => new BlobStorageService(
                serviceProvider.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(containerName).GetBlobClient(blobName)));

            return services;
        }
    }
}
