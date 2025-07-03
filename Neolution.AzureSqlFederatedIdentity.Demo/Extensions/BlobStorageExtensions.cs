namespace Neolution.AzureSqlFederatedIdentity.Demo.Extensions
{
    using System;
    using Azure.Core;
    using Azure.Storage.Blobs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Demo.Authentication;
    using Neolution.AzureSqlFederatedIdentity.Demo.Services;

    /// <summary>
    /// Extension methods for registering Blob Storage services in the Demo.
    /// </summary>
    public static class BlobStorageExtensions
    {
        /// <summary>
        /// Registers TokenCredential and BlobServiceClient for Blob Storage.
        /// </summary>
        public static IServiceCollection AddBlobStorageClient(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register TokenCredential based on IBlobStorageTokenProvider
            services.AddScoped<TokenCredential>(sp =>
                new DelegateTokenCredential(async (ctx, ct) =>
                {
                    var tokenProvider = sp.GetRequiredService<IBlobStorageTokenProvider>();
                    var token = await tokenProvider.GetBlobStorageAccessTokenAsync(ct);
                    return new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(55));
                }));

            // Configure BlobServiceClient
            var endpoint = configuration["BlobStorageTestFile:Endpoint"] ?? throw new InvalidOperationException("Missing configuration 'BlobStorageTestFile:Endpoint'.");

            services.AddScoped(sp => new BlobServiceClient(new Uri(endpoint), sp.GetRequiredService<TokenCredential>()));

            // Register blob storage service implementation
            services.AddScoped<IBlobStorageService, BlobStorageService>();

            return services;
        }
    }
}
