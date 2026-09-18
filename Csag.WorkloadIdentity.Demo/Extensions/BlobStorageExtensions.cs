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

            // The library's TokenCredential adapter serves the client from the held Blob Storage token and reports
            // that token's real expiry, so the Azure SDK asks for a new one only when the provider refreshes it.
            services.AddSingleton(serviceProvider => new BlobServiceClient(
                new Uri(endpoint),
                new WorkloadIdentityTokenCredential(serviceProvider.GetRequiredService<IBlobStorageTokenProvider>())));
            services.AddSingleton<IBlobStorageService>(serviceProvider => new BlobStorageService(serviceProvider.GetRequiredService<BlobServiceClient>(), filePath));

            return services;
        }
    }
}
