namespace Neolution.WorkloadIdentity
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Neolution.WorkloadIdentity.Abstractions;
    using Neolution.WorkloadIdentity.Internal;
    using Neolution.WorkloadIdentity.Internal.Services.Azure;
    using Neolution.WorkloadIdentity.Internal.Services.Google;
    using Neolution.WorkloadIdentity.Options;

    /// <summary>
    /// Provides extension methods for configuring Azure Workload Identity services.
    /// </summary>
    public static class WorkloadIdentityExtensions
    {
        /// <summary>
        /// Adds Azure Workload Identity services with default options.
        /// Registers all supported token providers (e.g., Azure SQL, Blob Storage).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddWorkloadIdentity(this IServiceCollection services)
        {
            RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Adds Azure Workload Identity support, binds configuration, and registers services
        /// for all supported token scopes (e.g., Azure SQL, Blob Storage).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddWorkloadIdentity(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ConfigureWorkloadIdentityOptions(services, configuration);
            RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Configures workload identity options for all supported token scopes and providers.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        private static void ConfigureWorkloadIdentityOptions(IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection("Neolution.WorkloadIdentity");
            ConfigureResourceTypeOptions(WorkloadIdentityResourceType.AzureSql, section, services);
            ConfigureResourceTypeOptions(WorkloadIdentityResourceType.BlobStorage, section, services);

            // Register option validators
            services.AddSingleton<IValidateOptions<WorkloadIdentityOptions>, WorkloadIdentityOptionsValidator>();
            services.AddSingleton<IValidateOptions<AzureSqlOptions>, AzureSqlOptionsValidator>();
            services.AddSingleton<IValidateOptions<BlobStorageOptions>, BlobStorageOptionsValidator>();
            services.AddSingleton<IValidateOptions<GoogleOptions>, GoogleOptionsValidator>();
            services.AddSingleton<IValidateOptions<ManagedIdentityOptions>, ManagedIdentityOptionsValidator>();
        }

        /// <summary>
        /// Configures token resourceType options for a specific token resourceType (e.g., Azure SQL, Blob Storage).
        /// </summary>
        /// <param name="resourceType">The token resourceType to configure.</param>
        /// <param name="configuration">The root configuration section.</param>
        /// <param name="services">The service collection.</param>
        private static void ConfigureResourceTypeOptions(WorkloadIdentityResourceType resourceType, IConfiguration configuration, IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(resourceType);

            var scopeName = resourceType.ToString();
            if (string.IsNullOrWhiteSpace(scopeName))
            {
                throw new ArgumentException("Scope name cannot be null or whitespace.", nameof(resourceType));
            }

            var scopeSection = configuration.GetSection(scopeName);

            switch (resourceType)
            {
                case WorkloadIdentityResourceType.AzureSql:
                    services.Configure<AzureSqlOptions>(options => scopeSection.Bind(options));
                    break;
                case WorkloadIdentityResourceType.BlobStorage:
                    services.Configure<BlobStorageOptions>(options => scopeSection.Bind(options));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported resource type section: {scopeName}");
            }

            ConfigureProviderOptions(services, scopeName, scopeSection, WorkloadIdentityProvider.Google);
            ConfigureProviderOptions(services, scopeName, scopeSection, WorkloadIdentityProvider.ManagedIdentity);
        }

        /// <summary>
        /// Configures provider options for a specific workload identity provider and token scope.
        /// Supports both Azure Managed Identity and Google federated identity providers.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="scopeName">The name of the token scope.</param>
        /// <param name="scopeSection">The configuration section for the scope.</param>
        /// <param name="provider">The workload identity provider to configure.</param>
        private static void ConfigureProviderOptions(IServiceCollection services, string scopeName, IConfiguration scopeSection, WorkloadIdentityProvider provider)
        {
            var providerName = provider.ToString();
            var providerSection = scopeSection.GetSection(providerName);

            switch (provider)
            {
                case WorkloadIdentityProvider.Google:
                    services.Configure<GoogleOptions>(scopeName, opts => providerSection.Bind(opts));
                    break;
                case WorkloadIdentityProvider.ManagedIdentity:
                    services.Configure<ManagedIdentityOptions>(scopeName, opts => providerSection.Bind(opts));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported workload identity provider: {provider}");
            }
        }

        /// <summary>
        /// Registers the core workload identity services in the dependency injection container,
        /// including support for Azure SQL and Blob Storage token providers.
        /// </summary>
        /// <param name="services">The service collection.</param>
        private static void RegisterServices(IServiceCollection services)
        {
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
                                s.GetRequiredService<IOptionsMonitor<ManagedIdentityOptions>>())
                        },
                        {
                            WorkloadIdentityProvider.Google,
                            s => new GoogleFederatedTokenExchanger(
                                s.GetRequiredService<ILogger<GoogleFederatedTokenExchanger>>(),
                                s.GetRequiredService<GoogleIdTokenProvider>(),
                                s.GetRequiredService<IOptionsMonitor<GoogleOptions>>())
                        },
                    }));

            services.AddMemoryCache();
            services.AddHostedService<TokenRefreshService>();

            services.AddSingleton<IAzureSqlTokenProvider, AzureSqlTokenProvider>();
            services.AddSingleton<IBlobStorageTokenProvider, BlobStorageTokenProvider>();
        }
    }
}
