namespace Csag.WorkloadIdentity.UnitTests
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Internal;
    using Microsoft.Extensions.Logging.Abstractions;
    using NSubstitute;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="AzureSqlTokenExchanger"/> class.
    /// </summary>
    public class AzureSqlTokenExchangerTests
    {
        /// <summary>
        /// The Azure AD tenant ID passed to the exchanger.
        /// </summary>
        private const string TenantId = "tenant";

        /// <summary>
        /// The Azure AD client ID passed to the exchanger.
        /// </summary>
        private const string ClientId = "client";

        /// <summary>
        /// The scope that grants access to Azure SQL.
        /// </summary>
        private const string AzureSqlScope = "https://database.windows.net/.default";

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
        /// The exchanger under test.
        /// </summary>
        private readonly AzureSqlTokenExchanger exchanger;

        /// <summary>
        /// The client assertion callback the exchanger handed to the factory, once a credential was created.
        /// </summary>
        private Func<CancellationToken, Task<string>>? assertionCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSqlTokenExchangerTests"/> class.
        /// </summary>
        public AzureSqlTokenExchangerTests()
        {
            this.credentialFactory
                .Create(TenantId, ClientId, Arg.Do<Func<CancellationToken, Task<string>>>(callback => this.assertionCallback = callback))
                .Returns(this.credential);
            this.exchanger = new AzureSqlTokenExchanger(NullLogger<AzureSqlTokenExchanger>.Instance, this.googleIdTokenProvider, this.credentialFactory);
        }

        /// <summary>
        /// Verifies that the exchanger requests the Azure SQL scope and returns the credential's token.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_Credential_When_ExchangeClientAssertionForAzureTokenAsync_Then_RequestsAzureSqlScopeAndReturnsToken()
        {
            // Arrange
            var expected = new AccessToken("access-token", DateTimeOffset.UtcNow.AddHours(1));
            this.credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), Arg.Any<CancellationToken>()).Returns(expected);

            // Act
            var result = await this.exchanger.ExchangeClientAssertionForAzureTokenAsync(TenantId, ClientId, CancellationToken.None);

            // Assert
            result.Token.ShouldBe(expected.Token);
            result.ExpiresOn.ShouldBe(expected.ExpiresOn);
            await this.credential.Received(1).GetTokenAsync(Arg.Is<TokenRequestContext>(context => context.Scopes.Single() == AzureSqlScope), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that repeated exchanges for the same tenant and client reuse one credential.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_TwoExchangesForSameTenantAndClient_When_ExchangeClientAssertionForAzureTokenAsync_Then_CreatesCredentialOnce()
        {
            // Arrange
            this.credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), Arg.Any<CancellationToken>())
                .Returns(new AccessToken("access-token", DateTimeOffset.UtcNow.AddHours(1)));

            // Act
            await this.exchanger.ExchangeClientAssertionForAzureTokenAsync(TenantId, ClientId, CancellationToken.None);
            await this.exchanger.ExchangeClientAssertionForAzureTokenAsync(TenantId, ClientId, CancellationToken.None);

            // Assert
            this.credentialFactory.Received(1).Create(TenantId, ClientId, Arg.Any<Func<CancellationToken, Task<string>>>());
        }

        /// <summary>
        /// Verifies that the client assertion callback forwards the cancellation token the credential passes to it,
        /// not the token of the call that created the credential.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_CreatedCredential_When_CredentialInvokesAssertionCallback_Then_GoogleProviderReceivesCredentialsCancellationToken()
        {
            // Arrange
            this.credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), Arg.Any<CancellationToken>())
                .Returns(new AccessToken("access-token", DateTimeOffset.UtcNow.AddHours(1)));
            this.googleIdTokenProvider.GetIdTokenAsync(Arg.Any<CancellationToken>()).Returns("id-token");
            using var callerCancellation = new CancellationTokenSource();
            using var credentialCancellation = new CancellationTokenSource();
            await this.exchanger.ExchangeClientAssertionForAzureTokenAsync(TenantId, ClientId, callerCancellation.Token);

            // Act
            var assertion = await this.assertionCallback.ShouldNotBeNull()(credentialCancellation.Token);

            // Assert
            assertion.ShouldBe("id-token");
            await this.googleIdTokenProvider.Received(1).GetIdTokenAsync(credentialCancellation.Token);
            await this.googleIdTokenProvider.DidNotReceive().GetIdTokenAsync(callerCancellation.Token);
        }
    }
}
