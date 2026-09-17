namespace Csag.AzureSqlFederatedIdentity.UnitTests
{
    using Csag.AzureSqlFederatedIdentity.Abstractions;
    using Csag.AzureSqlFederatedIdentity.Internal.Services;
    using Csag.AzureSqlFederatedIdentity.Options;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="FederatedIdentityServiceCollectionExtensions"/> class.
    /// </summary>
    public class FederatedIdentityServiceCollectionExtensionsTests
    {
        /// <summary>
        /// The Azure AD tenant ID in the test configuration.
        /// </summary>
        private const string TenantId = "tenant";

        /// <summary>
        /// The Azure AD client ID in the test configuration.
        /// </summary>
        private const string ClientId = "client";

        /// <summary>
        /// The Google service account email in the test configuration.
        /// </summary>
        private const string ServiceAccountEmail = "sa@example.iam.gserviceaccount.com";

        /// <summary>
        /// The refresh-ahead window in the test configuration.
        /// </summary>
        private static readonly TimeSpan RefreshAheadWindow = TimeSpan.FromMinutes(7);

        /// <summary>
        /// Verifies that the parameterless overload binds the default section of the registered configuration and
        /// wires up the token provider and the refresh service.
        /// </summary>
        [Fact]
        public void Given_ConfigurationInContainer_When_AddAzureSqlFederatedIdentity_Then_BindsOptionsAndResolvesPipeline()
        {
            // Arrange
            var services = CreateServices(CreateValidConfiguration());

            // Act
            services.AddAzureSqlFederatedIdentity();
            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            var options = serviceProvider.GetRequiredService<IOptions<AzureSqlFederatedIdentityOptions>>().Value;
            options.TenantId.ShouldBe(TenantId);
            options.ClientId.ShouldBe(ClientId);
            options.Google.ShouldNotBeNull().ServiceAccountEmail.ShouldBe(ServiceAccountEmail);
            options.RefreshAheadWindow.ShouldBe(RefreshAheadWindow);
            options.EnableBackgroundRefresh.ShouldBeFalse();
            serviceProvider.GetRequiredService<IAzureSqlTokenProvider>().ShouldBeOfType<AzureSqlTokenProvider>();
            serviceProvider.GetServices<IHostedService>().ShouldHaveSingleItem().ShouldBeOfType<AzureSqlTokenRefreshService>();
        }

        /// <summary>
        /// Verifies that missing configuration fails host startup with a validation error naming the missing value.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_MissingConfiguration_When_HostStarts_Then_ThrowsOptionsValidationException()
        {
            // Arrange
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.Services.AddAzureSqlFederatedIdentity();
            using var host = builder.Build();

            // Act
            var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());

            // Assert
            exception.Message.ShouldContain(nameof(AzureSqlFederatedIdentityOptions.TenantId));
        }

        /// <summary>
        /// Verifies that a token provider registered by the consumer beforehand is kept.
        /// </summary>
        [Fact]
        public void Given_ConsumerRegisteredTokenProvider_When_AddAzureSqlFederatedIdentity_Then_ConsumerRegistrationWins()
        {
            // Arrange
            var consumerProvider = Substitute.For<IAzureSqlTokenProvider>();
            var services = CreateServices(CreateValidConfiguration());
            services.AddSingleton(consumerProvider);

            // Act
            services.AddAzureSqlFederatedIdentity();
            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<IAzureSqlTokenProvider>().ShouldBeSameAs(consumerProvider);
        }

        /// <summary>
        /// Verifies that registering twice does not duplicate the services or the hosted service.
        /// </summary>
        [Fact]
        public void Given_CalledTwice_When_AddAzureSqlFederatedIdentity_Then_RegistersEachServiceOnce()
        {
            // Arrange
            var services = CreateServices(CreateValidConfiguration());

            // Act
            services.AddAzureSqlFederatedIdentity();
            services.AddAzureSqlFederatedIdentity();

            // Assert
            services.Count(descriptor => descriptor.ServiceType == typeof(IAzureSqlTokenProvider)).ShouldBe(1);
            services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)).ShouldBe(1);
            services.Count(descriptor => descriptor.ServiceType == typeof(IValidateOptions<AzureSqlFederatedIdentityOptions>)).ShouldBe(1);
        }

        /// <summary>
        /// Verifies that the configure-action overload applies the options without any configuration in the container.
        /// </summary>
        [Fact]
        public void Given_ConfigureAction_When_AddAzureSqlFederatedIdentity_Then_AppliesOptions()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddAzureSqlFederatedIdentity(options =>
            {
                options.TenantId = TenantId;
                options.ClientId = ClientId;
                options.Google = new GoogleOptions { ServiceAccountEmail = ServiceAccountEmail };
            });
            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            var options = serviceProvider.GetRequiredService<IOptions<AzureSqlFederatedIdentityOptions>>().Value;
            options.TenantId.ShouldBe(TenantId);
            options.EnableBackgroundRefresh.ShouldBeTrue();
            serviceProvider.GetRequiredService<IAzureSqlTokenProvider>().ShouldBeOfType<AzureSqlTokenProvider>();
        }

        /// <summary>
        /// Verifies that the configuration overload binds from the given instance without any configuration in the container.
        /// </summary>
        [Fact]
        public void Given_ConfigurationInstance_When_AddAzureSqlFederatedIdentity_Then_BindsOptions()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddAzureSqlFederatedIdentity(CreateValidConfiguration());
            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            var options = serviceProvider.GetRequiredService<IOptions<AzureSqlFederatedIdentityOptions>>().Value;
            options.ClientId.ShouldBe(ClientId);
            options.RefreshAheadWindow.ShouldBe(RefreshAheadWindow);
            serviceProvider.GetRequiredService<IAzureSqlTokenProvider>().ShouldBeOfType<AzureSqlTokenProvider>();
        }

        /// <summary>
        /// Creates an in-memory configuration holding a complete default section.
        /// </summary>
        /// <returns>The configuration.</returns>
        private static IConfiguration CreateValidConfiguration()
        {
            var section = AzureSqlFederatedIdentityOptions.ConfigurationSectionName;
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{section}:TenantId"] = TenantId,
                    [$"{section}:ClientId"] = ClientId,
                    [$"{section}:Google:ServiceAccountEmail"] = ServiceAccountEmail,
                    [$"{section}:RefreshAheadWindow"] = RefreshAheadWindow.ToString(),
                    [$"{section}:EnableBackgroundRefresh"] = "false",
                })
                .Build();
        }

        /// <summary>
        /// Creates a service collection with the given configuration and logging registered, as a host would.
        /// </summary>
        /// <param name="configuration">The configuration to register.</param>
        /// <returns>The service collection.</returns>
        private static ServiceCollection CreateServices(IConfiguration configuration)
        {
            var services = new ServiceCollection();
            services.AddSingleton(configuration);
            services.AddLogging();
            return services;
        }
    }
}
