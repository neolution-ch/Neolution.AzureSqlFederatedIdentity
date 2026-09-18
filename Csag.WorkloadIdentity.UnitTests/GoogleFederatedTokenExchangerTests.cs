namespace Csag.WorkloadIdentity.UnitTests
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Internal;
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="GoogleFederatedTokenExchanger"/> class.
    /// </summary>
    public class GoogleFederatedTokenExchangerTests
    {
        /// <summary>
        /// The Microsoft Entra tenant ID both resources are configured with.
        /// </summary>
        private const string TenantId = "tenant";

        /// <summary>
        /// The client ID both resources are configured with.
        /// </summary>
        private const string ClientId = "client";

        /// <summary>
        /// The service account the Azure SQL resource federates through.
        /// </summary>
        private const string AzureSqlServiceAccountEmail = "sql@example.iam.gserviceaccount.com";

        /// <summary>
        /// The service account the Blob Storage resource federates through.
        /// </summary>
        private const string BlobStorageServiceAccountEmail = "blob@example.iam.gserviceaccount.com";

        /// <summary>
        /// The OAuth 2.0 scope of Azure SQL.
        /// </summary>
        private const string AzureSqlScope = "https://database.windows.net/.default";

        /// <summary>
        /// The OAuth 2.0 scope of Blob Storage.
        /// </summary>
        private const string BlobStorageScope = "https://storage.azure.com/.default";

        /// <summary>
        /// The substituted Google ID token provider.
        /// </summary>
        private readonly IGoogleIdTokenProvider googleIdTokenProvider = Substitute.For<IGoogleIdTokenProvider>();

        /// <summary>
        /// The substituted credential factory.
        /// </summary>
        private readonly IClientAssertionCredentialFactory credentialFactory = Substitute.For<IClientAssertionCredentialFactory>();

        /// <summary>
        /// The substituted credential the factory hands out.
        /// </summary>
        private readonly TokenCredential credential = Substitute.For<TokenCredential>();

        /// <summary>
        /// The client assertion callbacks the exchanger handed to the factory, in creation order.
        /// </summary>
        private readonly List<Func<CancellationToken, Task<string>>> assertionCallbacks = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleFederatedTokenExchangerTests"/> class.
        /// </summary>
        public GoogleFederatedTokenExchangerTests()
        {
            this.credentialFactory
                .Create(TenantId, ClientId, Arg.Do<Func<CancellationToken, Task<string>>>(this.assertionCallbacks.Add))
                .Returns(this.credential);
            this.credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), Arg.Any<CancellationToken>())
                .Returns(new AccessToken("access-token", DateTimeOffset.UtcNow.AddHours(1)));
        }

        /// <summary>
        /// Verifies that the exchanger identifies itself as the Google provider.
        /// </summary>
        [Fact]
        public void Given_Exchanger_When_Provider_Then_IsGoogle()
        {
            // Act
            var provider = this.CreateExchanger().Provider;

            // Assert
            provider.ShouldBe(WorkloadIdentityProvider.Google);
        }

        /// <summary>
        /// Verifies that the exchanger requests the resource's scope and returns the credential's token unchanged.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_Credential_When_ExchangeAsync_Then_RequestsResourceScopeAndReturnsToken()
        {
            // Arrange
            var expected = new AccessToken("sql-token", DateTimeOffset.UtcNow.AddHours(2));
            this.credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), Arg.Any<CancellationToken>()).Returns(expected);
            var exchanger = this.CreateExchanger();

            // Act
            var result = await exchanger.ExchangeAsync(TokenScope.AzureSql, CancellationToken.None);

            // Assert
            result.Token.ShouldBe(expected.Token);
            result.ExpiresOn.ShouldBe(expected.ExpiresOn);
            await this.credential.Received(1).GetTokenAsync(Arg.Is<TokenRequestContext>(context => context.Scopes.Single() == AzureSqlScope), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that repeated exchanges for the same resource reuse one credential.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_TwoExchangesForSameResource_When_ExchangeAsync_Then_CreatesCredentialOnce()
        {
            // Arrange
            var exchanger = this.CreateExchanger();

            // Act
            await exchanger.ExchangeAsync(TokenScope.AzureSql, CancellationToken.None);
            await exchanger.ExchangeAsync(TokenScope.AzureSql, CancellationToken.None);

            // Assert
            this.credentialFactory.Received(1).Create(TenantId, ClientId, Arg.Any<Func<CancellationToken, Task<string>>>());
        }

        /// <summary>
        /// Verifies that resources federating through different service accounts get their own credentials, each
        /// requesting its own scope.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_TwoResourcesWithDifferentServiceAccounts_When_ExchangeAsync_Then_CreatesOneCredentialEach()
        {
            // Arrange
            var exchanger = this.CreateExchanger();

            // Act
            await exchanger.ExchangeAsync(TokenScope.AzureSql, CancellationToken.None);
            await exchanger.ExchangeAsync(TokenScope.BlobStorage, CancellationToken.None);

            // Assert
            this.credentialFactory.Received(2).Create(TenantId, ClientId, Arg.Any<Func<CancellationToken, Task<string>>>());
            await this.credential.Received(1).GetTokenAsync(Arg.Is<TokenRequestContext>(context => context.Scopes.Single() == AzureSqlScope), Arg.Any<CancellationToken>());
            await this.credential.Received(1).GetTokenAsync(Arg.Is<TokenRequestContext>(context => context.Scopes.Single() == BlobStorageScope), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that the client assertion callback asks for the resource's service account and forwards the
        /// cancellation token the credential passes to it, not the token of the call that created the credential.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_CreatedCredential_When_CredentialInvokesAssertionCallback_Then_GoogleProviderReceivesServiceAccountAndCredentialsCancellationToken()
        {
            // Arrange
            this.googleIdTokenProvider.GetIdTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("id-token");
            using var callerCancellation = new CancellationTokenSource();
            using var credentialCancellation = new CancellationTokenSource();
            var exchanger = this.CreateExchanger();
            await exchanger.ExchangeAsync(TokenScope.BlobStorage, callerCancellation.Token);

            // Act
            var assertion = await this.assertionCallbacks.ShouldHaveSingleItem()(credentialCancellation.Token);

            // Assert
            assertion.ShouldBe("id-token");
            await this.googleIdTokenProvider.Received(1).GetIdTokenAsync(BlobStorageServiceAccountEmail, credentialCancellation.Token);
            await this.googleIdTokenProvider.DidNotReceive().GetIdTokenAsync(Arg.Any<string>(), callerCancellation.Token);
        }

        /// <summary>
        /// Verifies that exchanging for a resource without a section fails with a message naming the section.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_UnconfiguredResource_When_ExchangeAsync_Then_ThrowsInvalidOperationExceptionNamingSection()
        {
            // Arrange
            var exchanger = this.CreateExchanger(new WorkloadIdentityOptions { AzureSql = CreateGoogleResource(AzureSqlServiceAccountEmail) });

            // Act
            var exception = await Should.ThrowAsync<InvalidOperationException>(() => exchanger.ExchangeAsync(TokenScope.BlobStorage, CancellationToken.None));

            // Assert
            exception.Message.ShouldContain("Csag.WorkloadIdentity:BlobStorage");
        }

        /// <summary>
        /// Creates a resource section that federates through the given service account.
        /// </summary>
        /// <param name="serviceAccountEmail">The service account.</param>
        /// <returns>The section.</returns>
        private static WorkloadIdentityResourceOptions CreateGoogleResource(string serviceAccountEmail)
        {
            return new WorkloadIdentityResourceOptions
            {
                Provider = WorkloadIdentityProvider.Google,
                Google = new GoogleOptions
                {
                    TenantId = TenantId,
                    ClientId = ClientId,
                    ServiceAccountEmail = serviceAccountEmail,
                },
            };
        }

        /// <summary>
        /// Creates the exchanger under test, configured with both resources unless other options are given.
        /// </summary>
        /// <param name="options">The options, or <see langword="null"/> for both resources over Google federation.</param>
        /// <returns>The exchanger.</returns>
        private GoogleFederatedTokenExchanger CreateExchanger(WorkloadIdentityOptions? options = null)
        {
            options ??= new WorkloadIdentityOptions
            {
                AzureSql = CreateGoogleResource(AzureSqlServiceAccountEmail),
                BlobStorage = CreateGoogleResource(BlobStorageServiceAccountEmail),
            };

            return new GoogleFederatedTokenExchanger(Options.Create(options), this.googleIdTokenProvider, this.credentialFactory, NullLogger<GoogleFederatedTokenExchanger>.Instance);
        }
    }
}
