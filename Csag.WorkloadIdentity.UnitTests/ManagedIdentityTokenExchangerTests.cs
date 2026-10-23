namespace Csag.WorkloadIdentity.UnitTests
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Internal;
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="ManagedIdentityTokenExchanger"/> class.
    /// </summary>
    public class ManagedIdentityTokenExchangerTests
    {
        /// <summary>
        /// The client ID of the user-assigned managed identity used by the tests.
        /// </summary>
        private const string UserAssignedClientId = "11111111-2222-3333-4444-555555555555";

        /// <summary>
        /// The OAuth 2.0 scope of Azure SQL.
        /// </summary>
        private const string AzureSqlScope = "https://database.windows.net/.default";

        /// <summary>
        /// The OAuth 2.0 scope of Blob Storage.
        /// </summary>
        private const string BlobStorageScope = "https://storage.azure.com/.default";

        /// <summary>
        /// The substituted credential factory.
        /// </summary>
        private readonly IManagedIdentityCredentialFactory credentialFactory = Substitute.For<IManagedIdentityCredentialFactory>();

        /// <summary>
        /// The substituted credential the factory hands out.
        /// </summary>
        private readonly TokenCredential credential = Substitute.For<TokenCredential>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedIdentityTokenExchangerTests"/> class.
        /// </summary>
        public ManagedIdentityTokenExchangerTests()
        {
            this.credentialFactory.Create(Arg.Any<ManagedIdentityOptions>()).Returns(this.credential);
            this.credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), Arg.Any<CancellationToken>())
                .Returns(new AccessToken("access-token", DateTimeOffset.UtcNow.AddHours(1)));
        }

        /// <summary>
        /// Verifies that the exchanger identifies itself as the managed identity provider.
        /// </summary>
        [Fact]
        public void Given_Exchanger_When_Provider_Then_IsManagedIdentity()
        {
            // Act
            var provider = this.CreateExchanger(new WorkloadIdentityOptions { AzureSql = CreateSystemAssignedResource() }).Provider;

            // Assert
            provider.ShouldBe(WorkloadIdentityProvider.ManagedIdentity);
        }

        /// <summary>
        /// Verifies that a system-assigned identity gets a credential for it and the resource's scope is requested.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_SystemAssignedIdentity_When_ExchangeAsync_Then_CreatesCredentialForItAndRequestsScope()
        {
            // Arrange
            var expected = new AccessToken("sql-token", DateTimeOffset.UtcNow.AddHours(2));
            this.credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), Arg.Any<CancellationToken>()).Returns(expected);
            var exchanger = this.CreateExchanger(new WorkloadIdentityOptions { AzureSql = CreateSystemAssignedResource() });

            // Act
            var result = await exchanger.ExchangeAsync(TokenScope.AzureSql, CancellationToken.None);

            // Assert
            result.Token.ShouldBe(expected.Token);
            result.ExpiresOn.ShouldBe(expected.ExpiresOn);
            this.credentialFactory.Received(1).Create(Arg.Is<ManagedIdentityOptions>(identity => identity.UseSystemAssignedIdentity));
            await this.credential.Received(1).GetTokenAsync(Arg.Is<TokenRequestContext>(context => context.Scopes.Single() == AzureSqlScope), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that a user-assigned identity gets a credential for its client ID.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_UserAssignedIdentity_When_ExchangeAsync_Then_CreatesCredentialForClientId()
        {
            // Arrange
            var exchanger = this.CreateExchanger(new WorkloadIdentityOptions { BlobStorage = CreateUserAssignedResource() });

            // Act
            await exchanger.ExchangeAsync(TokenScope.BlobStorage, CancellationToken.None);

            // Assert
            this.credentialFactory.Received(1).Create(Arg.Is<ManagedIdentityOptions>(identity => !identity.UseSystemAssignedIdentity && identity.ClientId == UserAssignedClientId));
            await this.credential.Received(1).GetTokenAsync(Arg.Is<TokenRequestContext>(context => context.Scopes.Single() == BlobStorageScope), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that repeated exchanges for the same resource reuse one credential.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_TwoExchangesForSameResource_When_ExchangeAsync_Then_CreatesCredentialOnce()
        {
            // Arrange
            var exchanger = this.CreateExchanger(new WorkloadIdentityOptions { AzureSql = CreateUserAssignedResource() });

            // Act
            await exchanger.ExchangeAsync(TokenScope.AzureSql, CancellationToken.None);
            await exchanger.ExchangeAsync(TokenScope.AzureSql, CancellationToken.None);

            // Assert
            this.credentialFactory.Received(1).Create(Arg.Any<ManagedIdentityOptions>());
        }

        /// <summary>
        /// Verifies that two resources using the same identity share one credential while each requests its own scope.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_TwoResourcesWithSameIdentity_When_ExchangeAsync_Then_SharesOneCredential()
        {
            // Arrange
            var exchanger = this.CreateExchanger(new WorkloadIdentityOptions
            {
                AzureSql = CreateSystemAssignedResource(),
                BlobStorage = CreateSystemAssignedResource(),
            });

            // Act
            await exchanger.ExchangeAsync(TokenScope.AzureSql, CancellationToken.None);
            await exchanger.ExchangeAsync(TokenScope.BlobStorage, CancellationToken.None);

            // Assert
            this.credentialFactory.Received(1).Create(Arg.Any<ManagedIdentityOptions>());
            await this.credential.Received(1).GetTokenAsync(Arg.Is<TokenRequestContext>(context => context.Scopes.Single() == AzureSqlScope), Arg.Any<CancellationToken>());
            await this.credential.Received(1).GetTokenAsync(Arg.Is<TokenRequestContext>(context => context.Scopes.Single() == BlobStorageScope), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that exchanging for a resource without a section fails with a message naming the section.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_UnconfiguredResource_When_ExchangeAsync_Then_ThrowsInvalidOperationExceptionNamingSection()
        {
            // Arrange
            var exchanger = this.CreateExchanger(new WorkloadIdentityOptions { BlobStorage = CreateSystemAssignedResource() });

            // Act
            var exception = await Should.ThrowAsync<InvalidOperationException>(() => exchanger.ExchangeAsync(TokenScope.AzureSql, CancellationToken.None));

            // Assert
            exception.Message.ShouldContain("Csag.WorkloadIdentity:AzureSql");
            this.credentialFactory.DidNotReceiveWithAnyArgs().Create(null!);
        }

        /// <summary>
        /// Creates a resource section that uses the system-assigned managed identity.
        /// </summary>
        /// <returns>The section.</returns>
        private static WorkloadIdentityResourceOptions CreateSystemAssignedResource()
        {
            return new WorkloadIdentityResourceOptions
            {
                Provider = WorkloadIdentityProvider.ManagedIdentity,
                ManagedIdentity = new ManagedIdentityOptions { UseSystemAssignedIdentity = true },
            };
        }

        /// <summary>
        /// Creates a resource section that uses the user-assigned managed identity.
        /// </summary>
        /// <returns>The section.</returns>
        private static WorkloadIdentityResourceOptions CreateUserAssignedResource()
        {
            return new WorkloadIdentityResourceOptions
            {
                Provider = WorkloadIdentityProvider.ManagedIdentity,
                ManagedIdentity = new ManagedIdentityOptions { ClientId = UserAssignedClientId },
            };
        }

        /// <summary>
        /// Creates the exchanger under test.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>The exchanger.</returns>
        private ManagedIdentityTokenExchanger CreateExchanger(WorkloadIdentityOptions options)
        {
            return new ManagedIdentityTokenExchanger(Options.Create(options), this.credentialFactory, NullLogger<ManagedIdentityTokenExchanger>.Instance);
        }
    }
}
