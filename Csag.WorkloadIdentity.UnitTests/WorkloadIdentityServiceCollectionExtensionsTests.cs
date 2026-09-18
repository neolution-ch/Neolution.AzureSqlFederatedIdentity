namespace Csag.WorkloadIdentity.UnitTests
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Internal;
    using Csag.WorkloadIdentity.Internal.Services;
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="WorkloadIdentityServiceCollectionExtensions"/> class.
    /// </summary>
    public class WorkloadIdentityServiceCollectionExtensionsTests
    {
        /// <summary>
        /// The Microsoft Entra tenant ID in the test configuration.
        /// </summary>
        private const string TenantId = "tenant";

        /// <summary>
        /// The client ID of the app registration in the test configuration.
        /// </summary>
        private const string ClientId = "client";

        /// <summary>
        /// The Google service account email in the test configuration.
        /// </summary>
        private const string ServiceAccountEmail = "sa@example.iam.gserviceaccount.com";

        /// <summary>
        /// The client ID of the user-assigned managed identity in the test configuration.
        /// </summary>
        private const string UserAssignedClientId = "11111111-2222-3333-4444-555555555555";

        /// <summary>
        /// The refresh-ahead window in the test configuration.
        /// </summary>
        private static readonly TimeSpan RefreshAheadWindow = TimeSpan.FromMinutes(7);

        /// <summary>
        /// Verifies that the parameterless overload binds the default section of the registered configuration and
        /// wires up both token providers, both exchangers and the refresh service.
        /// </summary>
        [Fact]
        public void Given_ConfigurationInContainer_When_AddWorkloadIdentity_Then_BindsOptionsAndResolvesPipeline()
        {
            // Arrange
            var services = CreateServices(CreateValidConfiguration());

            // Act
            services.AddWorkloadIdentity();
            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            var options = serviceProvider.GetRequiredService<IOptions<WorkloadIdentityOptions>>().Value;
            var azureSql = options.AzureSql.ShouldNotBeNull();
            azureSql.Provider.ShouldBe(WorkloadIdentityProvider.Google);
            var google = azureSql.Google.ShouldNotBeNull();
            google.TenantId.ShouldBe(TenantId);
            google.ClientId.ShouldBe(ClientId);
            google.ServiceAccountEmail.ShouldBe(ServiceAccountEmail);
            var blobStorage = options.BlobStorage.ShouldNotBeNull();
            blobStorage.Provider.ShouldBe(WorkloadIdentityProvider.ManagedIdentity);
            blobStorage.ManagedIdentity.ShouldNotBeNull().ClientId.ShouldBe(UserAssignedClientId);
            options.RefreshAheadWindow.ShouldBe(RefreshAheadWindow);
            options.EnableBackgroundRefresh.ShouldBeFalse();
            serviceProvider.GetRequiredService<IAzureSqlTokenProvider>().ShouldBeOfType<AzureSqlTokenProvider>();
            serviceProvider.GetRequiredService<IBlobStorageTokenProvider>().ShouldBeOfType<BlobStorageTokenProvider>();
            serviceProvider.GetServices<IWorkloadIdentityTokenExchanger>().Select(exchanger => exchanger.GetType())
                .ShouldBe([typeof(GoogleFederatedTokenExchanger), typeof(ManagedIdentityTokenExchanger)], ignoreOrder: true);
            serviceProvider.GetServices<IHostedService>().ShouldHaveSingleItem().ShouldBeOfType<TokenRefreshService>();
        }

        /// <summary>
        /// Verifies that missing configuration fails host startup with a validation error naming the requirement.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_MissingConfiguration_When_HostStarts_Then_ThrowsOptionsValidationException()
        {
            // Arrange
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.Services.AddWorkloadIdentity();
            using var host = builder.Build();

            // Act
            var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());

            // Assert
            exception.Message.ShouldContain("At least one resource section");
        }

        /// <summary>
        /// Verifies that an incomplete resource section fails host startup with a validation error naming the path.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_IncompleteResourceSection_When_HostStarts_Then_ThrowsOptionsValidationExceptionNamingPath()
        {
            // Arrange
            var section = WorkloadIdentityOptions.ConfigurationSectionName;
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{section}:AzureSql:Provider"] = nameof(WorkloadIdentityProvider.Google),
                [$"{section}:AzureSql:Google:TenantId"] = TenantId,
            });
            builder.Services.AddWorkloadIdentity();
            using var host = builder.Build();

            // Act
            var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());

            // Assert
            exception.Message.ShouldContain("AzureSql:Google:ClientId must be provided.");
            exception.Message.ShouldContain("AzureSql:Google:ServiceAccountEmail must be provided.");
        }

        /// <summary>
        /// Verifies that a token provider registered by the consumer beforehand is kept.
        /// </summary>
        [Fact]
        public void Given_ConsumerRegisteredTokenProvider_When_AddWorkloadIdentity_Then_ConsumerRegistrationWins()
        {
            // Arrange
            var consumerProvider = Substitute.For<IAzureSqlTokenProvider>();
            var services = CreateServices(CreateValidConfiguration());
            services.AddSingleton(consumerProvider);

            // Act
            services.AddWorkloadIdentity();
            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<IAzureSqlTokenProvider>().ShouldBeSameAs(consumerProvider);
            serviceProvider.GetRequiredService<IBlobStorageTokenProvider>().ShouldBeOfType<BlobStorageTokenProvider>();
        }

        /// <summary>
        /// Verifies that registering twice does not duplicate the services or the hosted service.
        /// </summary>
        [Fact]
        public void Given_CalledTwice_When_AddWorkloadIdentity_Then_RegistersEachServiceOnce()
        {
            // Arrange
            var services = CreateServices(CreateValidConfiguration());

            // Act
            services.AddWorkloadIdentity();
            services.AddWorkloadIdentity();

            // Assert
            services.Count(descriptor => descriptor.ServiceType == typeof(IAzureSqlTokenProvider)).ShouldBe(1);
            services.Count(descriptor => descriptor.ServiceType == typeof(IBlobStorageTokenProvider)).ShouldBe(1);
            services.Count(descriptor => descriptor.ServiceType == typeof(IWorkloadIdentityTokenExchanger)).ShouldBe(2);
            services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)).ShouldBe(1);
            services.Count(descriptor => descriptor.ServiceType == typeof(IValidateOptions<WorkloadIdentityOptions>)).ShouldBe(1);
        }

        /// <summary>
        /// Verifies that the configure-action overload applies the options without any configuration in the container.
        /// </summary>
        [Fact]
        public void Given_ConfigureAction_When_AddWorkloadIdentity_Then_AppliesOptions()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddWorkloadIdentity(options => options.AzureSql = CreateGoogleResource());
            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            var options = serviceProvider.GetRequiredService<IOptions<WorkloadIdentityOptions>>().Value;
            options.AzureSql.ShouldNotBeNull().Google.ShouldNotBeNull().TenantId.ShouldBe(TenantId);
            options.BlobStorage.ShouldBeNull();
            options.EnableBackgroundRefresh.ShouldBeTrue();
            serviceProvider.GetRequiredService<IAzureSqlTokenProvider>().ShouldBeOfType<AzureSqlTokenProvider>();
        }

        /// <summary>
        /// Verifies that the configuration overload binds from the given instance without any configuration in the container.
        /// </summary>
        [Fact]
        public void Given_ConfigurationInstance_When_AddWorkloadIdentity_Then_BindsOptions()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddWorkloadIdentity(CreateValidConfiguration());
            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            var options = serviceProvider.GetRequiredService<IOptions<WorkloadIdentityOptions>>().Value;
            options.AzureSql.ShouldNotBeNull().Google.ShouldNotBeNull().ClientId.ShouldBe(ClientId);
            options.RefreshAheadWindow.ShouldBe(RefreshAheadWindow);
            serviceProvider.GetRequiredService<IBlobStorageTokenProvider>().ShouldBeOfType<BlobStorageTokenProvider>();
        }

        /// <summary>
        /// Verifies that resolving the provider of a resource without a section fails with a message naming the
        /// section, while the configured resource's provider still resolves.
        /// </summary>
        [Fact]
        public void Given_UnconfiguredResource_When_ResolvingItsTokenProvider_Then_ThrowsInvalidOperationExceptionNamingSection()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddWorkloadIdentity(options => options.AzureSql = CreateGoogleResource());
            using var serviceProvider = services.BuildServiceProvider();

            // Act
            var exception = Should.Throw<InvalidOperationException>(() => serviceProvider.GetRequiredService<IBlobStorageTokenProvider>());

            // Assert
            exception.Message.ShouldContain("Csag.WorkloadIdentity:BlobStorage");
            serviceProvider.GetRequiredService<IAzureSqlTokenProvider>().ShouldBeOfType<AzureSqlTokenProvider>();
        }

        /// <summary>
        /// Verifies that the overloads reject a missing configuration or configure action.
        /// </summary>
        [Fact]
        public void Given_NullArguments_When_AddWorkloadIdentity_Then_ThrowsArgumentNullException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var configurationException = Should.Throw<ArgumentNullException>(() => services.AddWorkloadIdentity((IConfiguration)null!));
            var actionException = Should.Throw<ArgumentNullException>(() => services.AddWorkloadIdentity((Action<WorkloadIdentityOptions>)null!));

            // Assert
            configurationException.ParamName.ShouldBe("configuration");
            actionException.ParamName.ShouldBe("configureOptions");
        }

        /// <summary>
        /// Verifies that a resource selecting the managed identity provider is served through the managed identity
        /// credential seam with its configured identity and scope.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_ManagedIdentityResourceAndSubstitutedCredentialFactory_When_GetBlobStorageAccessTokenAsync_Then_ReturnsCredentialsToken()
        {
            // Arrange
            var credential = Substitute.For<TokenCredential>();
            credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), Arg.Any<CancellationToken>())
                .Returns(new AccessToken("managed-identity-token", DateTimeOffset.UtcNow.AddHours(1)));
            var credentialFactory = Substitute.For<IManagedIdentityCredentialFactory>();
            credentialFactory.Create(Arg.Any<ManagedIdentityOptions>()).Returns(credential);
            var services = CreateServices(CreateValidConfiguration());
            services.AddSingleton(credentialFactory);
            services.AddWorkloadIdentity();
            using var serviceProvider = services.BuildServiceProvider();

            // Act
            var token = await serviceProvider.GetRequiredService<IBlobStorageTokenProvider>().GetBlobStorageAccessTokenAsync(CancellationToken.None);

            // Assert
            token.ShouldBe("managed-identity-token");
            credentialFactory.Received(1).Create(Arg.Is<ManagedIdentityOptions>(identity => identity.ClientId == UserAssignedClientId));
            await credential.Received(1).GetTokenAsync(Arg.Is<TokenRequestContext>(context => context.Scopes.Single() == "https://storage.azure.com/.default"), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that a resource selecting the Google provider is served through the client assertion credential
        /// seam with its configured identity and scope.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_GoogleResourceAndSubstitutedCredentialFactory_When_GetAzureSqlAccessTokenAsync_Then_ReturnsCredentialsToken()
        {
            // Arrange
            var credential = Substitute.For<TokenCredential>();
            credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), Arg.Any<CancellationToken>())
                .Returns(new AccessToken("federated-token", DateTimeOffset.UtcNow.AddHours(1)));
            var credentialFactory = Substitute.For<IClientAssertionCredentialFactory>();
            credentialFactory.Create(TenantId, ClientId, Arg.Any<Func<CancellationToken, Task<string>>>()).Returns(credential);
            var services = CreateServices(CreateValidConfiguration());
            services.AddSingleton(credentialFactory);
            services.AddWorkloadIdentity();
            using var serviceProvider = services.BuildServiceProvider();

            // Act
            var token = await serviceProvider.GetRequiredService<IAzureSqlTokenProvider>().GetAzureSqlAccessTokenAsync(CancellationToken.None);

            // Assert
            token.ShouldBe("federated-token");
            await credential.Received(1).GetTokenAsync(Arg.Is<TokenRequestContext>(context => context.Scopes.Single() == "https://database.windows.net/.default"), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Creates an in-memory configuration holding a complete default section: Azure SQL over Google federation
        /// and Blob Storage over a user-assigned managed identity.
        /// </summary>
        /// <returns>The configuration.</returns>
        private static IConfiguration CreateValidConfiguration()
        {
            var section = WorkloadIdentityOptions.ConfigurationSectionName;
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{section}:AzureSql:Provider"] = nameof(WorkloadIdentityProvider.Google),
                    [$"{section}:AzureSql:Google:TenantId"] = TenantId,
                    [$"{section}:AzureSql:Google:ClientId"] = ClientId,
                    [$"{section}:AzureSql:Google:ServiceAccountEmail"] = ServiceAccountEmail,
                    [$"{section}:BlobStorage:Provider"] = nameof(WorkloadIdentityProvider.ManagedIdentity),
                    [$"{section}:BlobStorage:ManagedIdentity:ClientId"] = UserAssignedClientId,
                    [$"{section}:RefreshAheadWindow"] = RefreshAheadWindow.ToString(),
                    [$"{section}:EnableBackgroundRefresh"] = "false",
                })
                .Build();
        }

        /// <summary>
        /// Creates a resource section that federates through Google with every value set.
        /// </summary>
        /// <returns>The section.</returns>
        private static WorkloadIdentityResourceOptions CreateGoogleResource()
        {
            return new WorkloadIdentityResourceOptions
            {
                Provider = WorkloadIdentityProvider.Google,
                Google = new GoogleOptions
                {
                    TenantId = TenantId,
                    ClientId = ClientId,
                    ServiceAccountEmail = ServiceAccountEmail,
                },
            };
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
