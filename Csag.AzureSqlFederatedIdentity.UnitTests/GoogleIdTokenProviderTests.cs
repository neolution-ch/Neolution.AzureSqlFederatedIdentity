namespace Csag.AzureSqlFederatedIdentity.UnitTests
{
    using Csag.AzureSqlFederatedIdentity.Internal;
    using Csag.AzureSqlFederatedIdentity.Options;
    using Google.Cloud.Iam.Credentials.V1;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="GoogleIdTokenProvider"/> class.
    /// </summary>
    public class GoogleIdTokenProviderTests
    {
        /// <summary>
        /// The service account the provider under test impersonates.
        /// </summary>
        private const string ServiceAccountEmail = "sa@example.iam.gserviceaccount.com";

        /// <summary>
        /// The ID token the substituted client hands out.
        /// </summary>
        private const string IdToken = "id-token";

        /// <summary>
        /// The substituted IAM Credentials client.
        /// </summary>
        private readonly IAMCredentialsClient client = Substitute.For<IAMCredentialsClient>();

        /// <summary>
        /// The substituted client factory.
        /// </summary>
        private readonly IIamCredentialsClientFactory clientFactory = Substitute.For<IIamCredentialsClientFactory>();

        /// <summary>
        /// The provider under test.
        /// </summary>
        private readonly GoogleIdTokenProvider provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleIdTokenProviderTests"/> class.
        /// </summary>
        public GoogleIdTokenProviderTests()
        {
            this.clientFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(this.client);
            this.provider = new GoogleIdTokenProvider(CreateOptions(), this.clientFactory, NullLogger<GoogleIdTokenProvider>.Instance);
        }

        /// <summary>
        /// Verifies that the provider requests a token for the configured service account with the Azure AD audience.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_Client_When_GetIdTokenAsync_Then_RequestsTokenForServiceAccountWithAzureAudience()
        {
            // Arrange
            this.SetupGeneratedToken(IdToken);

            // Act
            var result = await this.provider.GetIdTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(IdToken);
            await this.client.Received(1).GenerateIdTokenAsync(
                Arg.Is<GenerateIdTokenRequest>(request =>
                    request.Name == $"projects/-/serviceAccounts/{ServiceAccountEmail}"
                    && request.Audience == "api://AzureADTokenExchange"
                    && !request.IncludeEmail),
                Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that repeated calls reuse one client.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_TwoCalls_When_GetIdTokenAsync_Then_CreatesClientOnce()
        {
            // Arrange
            this.SetupGeneratedToken(IdToken);

            // Act
            await this.provider.GetIdTokenAsync(CancellationToken.None);
            await this.provider.GetIdTokenAsync(CancellationToken.None);

            // Assert
            await this.clientFactory.Received(1).CreateAsync(Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that a failed client creation is not retained and the next call creates the client again.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_FailedClientCreation_When_GetIdTokenAsyncAgain_Then_RetriesCreation()
        {
            // Arrange
            this.clientFactory.CreateAsync(Arg.Any<CancellationToken>())
                .Returns(
                    _ => throw new InvalidOperationException("no application default credentials"),
                    _ => this.client);
            this.SetupGeneratedToken(IdToken);

            // Act
            var failure = await Should.ThrowAsync<InvalidOperationException>(() => this.provider.GetIdTokenAsync(CancellationToken.None));
            var result = await this.provider.GetIdTokenAsync(CancellationToken.None);

            // Assert
            failure.Message.ShouldBe("no application default credentials");
            result.ShouldBe(IdToken);
            await this.clientFactory.Received(2).CreateAsync(Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that a cancelled client creation is not retained and the next call creates the client again.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_CancelledClientCreation_When_GetIdTokenAsyncAgain_Then_RetriesCreation()
        {
            // Arrange
            this.clientFactory.CreateAsync(Arg.Any<CancellationToken>())
                .Returns(
                    _ => Task.FromCanceled<IAMCredentialsClient>(new CancellationToken(canceled: true)),
                    _ => Task.FromResult(this.client));
            this.SetupGeneratedToken(IdToken);

            // Act
            await Should.ThrowAsync<TaskCanceledException>(() => this.provider.GetIdTokenAsync(CancellationToken.None));
            var result = await this.provider.GetIdTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe(IdToken);
            await this.clientFactory.Received(2).CreateAsync(Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that a caller cancelling while the client is being created does not fail the other callers.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_FirstCallerCancels_When_ClientCreationCompletes_Then_SecondCallerGetsToken()
        {
            // Arrange
            var creation = new TaskCompletionSource<IAMCredentialsClient>();
            this.clientFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(creation.Task);
            this.SetupGeneratedToken(IdToken);
            using var firstCaller = new CancellationTokenSource();

            // Act
            var first = this.provider.GetIdTokenAsync(firstCaller.Token);
            var second = this.provider.GetIdTokenAsync(CancellationToken.None);
            await firstCaller.CancelAsync();
            await Should.ThrowAsync<TaskCanceledException>(() => first);
            creation.SetResult(this.client);
            var result = await second;

            // Assert
            result.ShouldBe(IdToken);
            await this.clientFactory.Received(1).CreateAsync(Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that an empty token in the response is rejected.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_EmptyTokenInResponse_When_GetIdTokenAsync_Then_ThrowsInvalidOperationException()
        {
            // Arrange
            this.SetupGeneratedToken(string.Empty);

            // Act
            var exception = await Should.ThrowAsync<InvalidOperationException>(() => this.provider.GetIdTokenAsync(CancellationToken.None));

            // Assert
            exception.Message.ShouldContain(ServiceAccountEmail);
        }

        /// <summary>
        /// Verifies that the provider cannot be constructed without the Google options.
        /// </summary>
        [Fact]
        public void Given_MissingGoogleOptions_When_Constructed_Then_ThrowsArgumentNullException()
        {
            // Arrange
            var options = Options.Create(new AzureSqlFederatedIdentityOptions());

            // Act
            var exception = Should.Throw<ArgumentNullException>(() => new GoogleIdTokenProvider(options, this.clientFactory, NullLogger<GoogleIdTokenProvider>.Instance));

            // Assert
            exception.ParamName.ShouldNotBeNull().ShouldContain(nameof(AzureSqlFederatedIdentityOptions.Google));
        }

        /// <summary>
        /// Creates options naming the test service account.
        /// </summary>
        /// <returns>The options.</returns>
        private static IOptions<AzureSqlFederatedIdentityOptions> CreateOptions()
        {
            return Options.Create(new AzureSqlFederatedIdentityOptions
            {
                Google = new GoogleOptions { ServiceAccountEmail = ServiceAccountEmail },
            });
        }

        /// <summary>
        /// Configures the substituted client to respond with the given token.
        /// </summary>
        /// <param name="token">The token to respond with.</param>
        private void SetupGeneratedToken(string token)
        {
            this.client.GenerateIdTokenAsync(Arg.Any<GenerateIdTokenRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GenerateIdTokenResponse { Token = token });
        }
    }
}
