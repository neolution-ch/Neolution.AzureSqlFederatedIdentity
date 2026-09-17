namespace Csag.AzureSqlFederatedIdentity
{
    using Csag.AzureSqlFederatedIdentity.Abstractions;
    using Csag.AzureSqlFederatedIdentity.Internal;
    using Csag.AzureSqlFederatedIdentity.Internal.Services;
    using Csag.AzureSqlFederatedIdentity.Options;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Provides extension methods for registering Azure SQL federated identity services.
    /// </summary>
    public static class FederatedIdentityServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Azure SQL federated identity services, binding the options from the
        /// <see cref="AzureSqlFederatedIdentityOptions.ConfigurationSectionName"/> section of the
        /// <see cref="IConfiguration"/> registered in the container. The options are validated when the host starts.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureSqlFederatedIdentity(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddOptions<AzureSqlFederatedIdentityOptions>()
                .BindConfiguration(AzureSqlFederatedIdentityOptions.ConfigurationSectionName)
                .ValidateOnStart();
            RegisterFederatedIdentityServices(services);
            return services;
        }

        /// <summary>
        /// Adds Azure SQL federated identity services with the options configured in code. The options are validated
        /// when the host starts.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">The action to configure options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureSqlFederatedIdentity(this IServiceCollection services, Action<AzureSqlFederatedIdentityOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.AddOptions<AzureSqlFederatedIdentityOptions>()
                .Configure(configureOptions)
                .ValidateOnStart();
            RegisterFederatedIdentityServices(services);
            return services;
        }

        /// <summary>
        /// Adds Azure SQL federated identity services, binding the options from the
        /// <see cref="AzureSqlFederatedIdentityOptions.ConfigurationSectionName"/> section of the given configuration.
        /// Use this when the configuration is not registered as <see cref="IConfiguration"/> in the container. The
        /// options are validated when the host starts.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureSqlFederatedIdentity(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<AzureSqlFederatedIdentityOptions>()
                .Bind(configuration.GetSection(AzureSqlFederatedIdentityOptions.ConfigurationSectionName))
                .ValidateOnStart();
            RegisterFederatedIdentityServices(services);
            return services;
        }

        /// <summary>
        /// Registers the token pipeline. Every registration is a Try* registration, so a consumer can substitute any
        /// service by registering its own implementation first, and repeated calls do not duplicate anything.
        /// </summary>
        /// <param name="services">The service collection.</param>
        private static void RegisterFederatedIdentityServices(IServiceCollection services)
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AzureSqlFederatedIdentityOptions>, AzureSqlFederatedIdentityOptionsValidator>());
            services.TryAddSingleton<IIamCredentialsClientFactory, IamCredentialsClientFactory>();
            services.TryAddSingleton<IClientAssertionCredentialFactory, ClientAssertionCredentialFactory>();
            services.TryAddSingleton<IGoogleIdTokenProvider, GoogleIdTokenProvider>();
            services.TryAddSingleton<IAzureSqlTokenExchanger, AzureSqlTokenExchanger>();
            services.TryAddSingleton<IAzureSqlTokenProvider, AzureSqlTokenProvider>();
            services.AddHostedService<AzureSqlTokenRefreshService>();
        }
    }
}
