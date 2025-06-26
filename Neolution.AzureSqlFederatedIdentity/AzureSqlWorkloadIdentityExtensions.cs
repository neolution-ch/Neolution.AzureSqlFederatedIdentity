namespace Neolution.AzureSqlFederatedIdentity
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Internal;
    using Neolution.AzureSqlFederatedIdentity.Internal.Exchangers;
    using Neolution.AzureSqlFederatedIdentity.Options;

    /// <summary>
    /// Provides extension methods for registering Azure SQL federated identity services.
    /// </summary>
    public static class AzureSqlWorkloadIdentityExtensions
    {
        /// <summary>
        /// Adds Azure SQL federated identity services with default options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureSqlWorkloadIdentity(this IServiceCollection services)
        {
            RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Adds Azure SQL federated identity services with custom options configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">The action to configure options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureSqlWorkloadIdentity(this IServiceCollection services, Action<AzureSqlOptions> configureOptions)
        {
            services.Configure(configureOptions);
            RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Adds Azure SQL workload identity support, binding configuration and registering services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureSqlWorkloadIdentity(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection("Neolution.WorkloadIdentity");
            services.Configure<AzureSqlOptions>(options => section.GetSection("AzureSql").Bind(options));
            services.Configure<BlobStorageOptions>(options => section.GetSection("BlobStorage").Bind(options));

            // Register named options for Google and ManagedIdentity for AzureSql
            services.Configure<GoogleOptions>("AzureSql", opts => section.GetSection("AzureSql:Google").Bind(opts));
            services.Configure<ManagedIdentityOptions>("AzureSql", opts => section.GetSection("AzureSql:ManagedIdentity").Bind(opts));

            // Register named options for Google and ManagedIdentity for BlobStorage
            services.Configure<GoogleOptions>("BlobStorage", opts => section.GetSection("BlobStorage:Google").Bind(opts));
            services.Configure<ManagedIdentityOptions>("BlobStorage", opts => section.GetSection("BlobStorage:ManagedIdentity").Bind(opts));

            RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Registers the core Azure SQL federated identity services in the DI container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        private static void RegisterServices(IServiceCollection services)
        {
            services.AddMemoryCache();

            services.AddSingleton<GoogleIdTokenProvider>();

            services.AddSingleton<WorkloadIdentityTokenExchangerFactory>(sp =>
                new WorkloadIdentityTokenExchangerFactory(
                    sp,
                    new Dictionary<WorkloadIdentityProvider, Func<IServiceProvider, IWorkloadIdentityTokenExchanger>>
                    {
                        {
                            WorkloadIdentityProvider.ManagedIdentity,
                            s => new ManagedIdentityTokenExchanger(
                                s.GetRequiredService<ILogger<ManagedIdentityTokenExchanger>>(),
                                s.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<ManagedIdentityOptions>>())
                        },
                        {
                            WorkloadIdentityProvider.Google,
                            s => new GoogleFederatedTokenExchanger(
                                s.GetRequiredService<ILogger<GoogleFederatedTokenExchanger>>(),
                                s.GetRequiredService<GoogleIdTokenProvider>(),
                                s.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<GoogleOptions>>())
                        },
                    }));

            // Register the main Azure SQL token provider
            services.AddSingleton<IAzureSqlTokenProvider, AzureSqlTokenProvider>();
        }
    }
}
