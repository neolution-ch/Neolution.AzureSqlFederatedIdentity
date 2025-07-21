namespace Neolution.WorkloadIdentity.UnitTests
{
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using Neolution.WorkloadIdentity.Abstractions;
    using Neolution.WorkloadIdentity.Internal;
    using Neolution.WorkloadIdentity.Internal.Services.Azure;
    using Neolution.WorkloadIdentity.Options;
    using Shouldly;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="WorkloadIdentityExtensions"/> class.
    /// </summary>
    public class WorkloadIdentityExtensionsTests
    {
        /// <summary>
        /// Verifies that core services are registered without requiring configuration.
        /// </summary>
        [Fact]
        public void AddWorkloadIdentity_RegistersCoreServices_WithoutConfiguration()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddWorkloadIdentity();
            var provider = services.BuildServiceProvider();

            // Assert: Token provider registrations
            provider.GetService<IAzureSqlTokenProvider>().ShouldNotBeNull();
            provider.GetService<IBlobStorageTokenProvider>().ShouldNotBeNull();

            // Assert: Exchanger factory
            provider.GetService<WorkloadIdentityTokenExchangerFactory>().ShouldNotBeNull();

            // Assert: Credential exchangers
            provider.GetService<ManagedIdentityTokenExchanger>().ShouldNotBeNull();
            provider.GetService<GoogleFederatedTokenExchanger>().ShouldNotBeNull();

            // Assert: Validators
            provider.GetService<IValidateOptions<WorkloadIdentityOptions>>().ShouldNotBeNull();
            provider.GetService<IValidateOptions<AzureSqlOptions>>().ShouldNotBeNull();
            provider.GetService<IValidateOptions<BlobStorageOptions>>().ShouldNotBeNull();
            provider.GetService<IValidateOptions<GoogleOptions>>().ShouldNotBeNull();
            provider.GetService<IValidateOptions<ManagedIdentityOptions>>().ShouldNotBeNull();

            // Assert: Memory cache and hosted service
            provider.GetService<IMemoryCache>().ShouldNotBeNull();
            var hostedServices = provider.GetServices<IHostedService>();
            hostedServices.ShouldContain(s => s is TokenRefreshService);
        }

        /// <summary>
        /// Verifies that an exception is thrown if the configuration is null.
        /// </summary>
        [Fact]
        public void AddWorkloadIdentity_ThrowsIfConfigurationNull()
        {
            // Arrange
            var services = new ServiceCollection();
            IConfiguration? configuration = null;

            // Act & Assert
            Should.Throw<ArgumentNullException>(() => services.AddWorkloadIdentity(configuration!));
        }

        /// <summary>
        /// Verifies that options and services are registered when configuration is provided.
        /// </summary>
        [Fact]
        public void AddWorkloadIdentity_WithConfiguration_RegistersOptionsAndServices()
        {
            // Arrange
            var inMemoryConfig = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();
            var services = new ServiceCollection();

            // Act
            services.AddWorkloadIdentity(inMemoryConfig);
            var provider = services.BuildServiceProvider();

            // Assert: Options monitors
            provider.GetService<IOptionsMonitor<AzureSqlOptions>>().ShouldNotBeNull();
            provider.GetService<IOptionsMonitor<BlobStorageOptions>>().ShouldNotBeNull();
            provider.GetService<IOptionsMonitor<GoogleOptions>>().ShouldNotBeNull();
            provider.GetService<IOptionsMonitor<ManagedIdentityOptions>>().ShouldNotBeNull();

            // Re-assert core services
            provider.GetService<IAzureSqlTokenProvider>().ShouldNotBeNull();
            provider.GetService<IBlobStorageTokenProvider>().ShouldNotBeNull();
            provider.GetService<IMemoryCache>().ShouldNotBeNull();
        }
    }
}
