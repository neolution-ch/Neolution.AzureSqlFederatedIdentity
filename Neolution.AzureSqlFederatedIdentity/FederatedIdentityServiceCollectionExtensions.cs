namespace Neolution.AzureSqlFederatedIdentity
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Internal;
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
            // Bind root and sub-options via manual binding to ensure correct overloads
            const string configSectionKey = "Neolution.AzureSqlFederatedIdentity";
            services.Configure<AzureSqlFederatedIdentityOptions>(options => configuration.GetSection(configSectionKey).Bind(options));
            services.Configure<ManagedIdentityOptions>(options => configuration.GetSection($"{configSectionKey}:ManagedIdentity").Bind(options));
            services.Configure<GoogleOptions>(options => configuration.GetSection($"{configSectionKey}:Google").Bind(options));

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

            // Register token provider implementations for DI
            services.AddSingleton<IGoogleIdTokenProvider, GoogleIdTokenProvider>();
            services.AddSingleton<IAzureSqlTokenProvider, AzureSqlTokenProvider>();

            // Token exchanger factory based on Provider enum
            services.AddSingleton<ITokenExchanger>(sp =>
            {
                var root = sp.GetRequiredService<IOptions<AzureSqlFederatedIdentityOptions>>().Value;
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                switch (root.Provider)
                {
                    case FederatedIdentityProvider.ManagedIdentity:
                        var mi = sp.GetRequiredService<IOptions<ManagedIdentityOptions>>().Value;
                        var miLogger = loggerFactory.CreateLogger<ManagedIdentityTokenExchanger>();
                        return new ManagedIdentityTokenExchanger(miLogger, mi);

                    case FederatedIdentityProvider.Google:
                        var g = sp.GetRequiredService<IOptions<GoogleOptions>>().Value;
                        var gLogger = loggerFactory.CreateLogger<AzureSqlTokenExchanger>();
                        var googleProvider = sp.GetRequiredService<IGoogleIdTokenProvider>();
                        return new AzureSqlTokenExchanger(gLogger, googleProvider, g);

                    default:
                        throw new InvalidOperationException($"Unknown provider '{root.Provider}'.");
                }
            });
        }
    }
}
