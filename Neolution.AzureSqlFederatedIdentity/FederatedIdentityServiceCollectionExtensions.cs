namespace Neolution.AzureSqlFederatedIdentity
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Internal;
    using Neolution.AzureSqlFederatedIdentity.Internal.Services;
    using Neolution.AzureSqlFederatedIdentity.Options;

    /// <summary>
    /// Provides extension methods for registering Azure SQL federated identity services.
    /// </summary>
    public static class FederatedIdentityServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Azure SQL federated identity services with default options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureSqlFederatedIdentity(this IServiceCollection services)
        {
            RegisterFederatedIdentityServices(services);
            return services;
        }

        /// <summary>
        /// Adds Azure SQL federated identity services with custom options configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">The action to configure options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureSqlFederatedIdentity(this IServiceCollection services, Action<AzureSqlFederatedIdentityOptions> configureOptions)
        {
            services.Configure(configureOptions);
            RegisterFederatedIdentityServices(services);
            return services;
        }

        /// <summary>
        /// Adds Azure SQL federated identity services and configuration to the DI container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureSqlFederatedIdentity(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AzureSqlFederatedIdentityOptions>(options => configuration.GetSection("Neolution.AzureSqlFederatedIdentity").Bind(options));
            RegisterFederatedIdentityServices(services);
            return services;
        }

        /// <summary>
        /// Registers the core Azure SQL federated identity services in the DI container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        private static void RegisterFederatedIdentityServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddSingleton<IGoogleIdTokenProvider, GoogleIdTokenProvider>();
            services.AddSingleton<IAzureSqlTokenExchanger, AzureSqlTokenExchanger>();
            services.AddSingleton<IAzureSqlTokenProvider, AzureSqlTokenProvider>();
            services.AddHostedService<AzureSqlTokenRefreshService>();
            services.AddSingleton<IValidateOptions<AzureSqlFederatedIdentityOptions>, AzureSqlFederatedIdentityOptionsValidator>();
        }
    }
}
