namespace Neolution.AzureSqlFederatedIdentity
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
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
        /// Adds Azure SQL workload identity support, binding configuration and registering services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureSqlWorkloadIdentity(this IServiceCollection services, IConfiguration configuration)
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
            ConfigureTokenScopeOptions(services, section, TokenScope.AzureSql);
            ConfigureTokenScopeOptions(services, section, TokenScope.BlobStorage);
        }

        /// <summary>
        /// Configures token scope options for a specific scope.
        /// </summary>
        /// <typeparam name="TScope">The type of the token scope.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="rootSection">The root configuration section.</param>
        /// <param name="scope">The token scope to configure.</param>
        /// <exception cref="ArgumentNullException">Thrown when the scope is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the scope name is null or whitespace.</exception>
        private static void ConfigureTokenScopeOptions<TScope>(IServiceCollection services, IConfiguration rootSection, TScope scope)
        {
            ArgumentNullException.ThrowIfNull(scope);

            var scopeName = scope.ToString();
            if (string.IsNullOrWhiteSpace(scopeName))
            {
                throw new ArgumentException("Scope name cannot be null or whitespace.", nameof(scope));
            }

            var scopeSection = rootSection.GetSection(scopeName);

            switch (scopeName)
            {
                case nameof(TokenScope.AzureSql):
                    services.Configure<AzureSqlOptions>(options => scopeSection.Bind(options));
                    break;
                case nameof(TokenScope.BlobStorage):
                    services.Configure<BlobStorageOptions>(options => scopeSection.Bind(options));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported token scope: {scopeName}");
            }

            ConfigureProviderOptions(services, scopeName, scopeSection, WorkloadIdentityProvider.Google);
            ConfigureProviderOptions(services, scopeName, scopeSection, WorkloadIdentityProvider.ManagedIdentity);
        }

        /// <summary>
        /// Configures provider options for a specific workload identity provider.
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

            // Register the main Azure SQL token provider
            services.AddSingleton<IAzureSqlTokenProvider, AzureSqlTokenProvider>();
        }
    }
}
