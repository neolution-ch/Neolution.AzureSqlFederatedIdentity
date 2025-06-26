namespace Neolution.AzureSqlFederatedIdentity
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Neolution.AzureSqlFederatedIdentity.Internal;
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

            var section = configuration.GetSection("Neolution.WorkloadIdentity:AzureSql");
            services.Configure<AzureSqlOptions>(options => section.Bind(options));
            services.Configure<ManagedIdentityOptions>(options => section.GetSection("ManagedIdentity").Bind(options));
            services.Configure<GoogleOptions>(options => section.GetSection("Google").Bind(options));

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

            // Register token provider implementations for DI
            services.AddSingleton<GoogleIdTokenProvider>();
            services.AddSingleton<AzureSqlTokenProvider>();
        }
    }
}
