namespace Csag.WorkloadIdentity.UnitTests
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Abstractions;
    using NSubstitute;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="WorkloadIdentityTokenCredential"/> class.
    /// </summary>
    public class WorkloadIdentityTokenCredentialTests
    {
        /// <summary>
        /// The instant the provider's token expires.
        /// </summary>
        private static readonly DateTimeOffset ExpiresOn = new(2026, 1, 1, 13, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// The token request every test makes; its scopes play no role.
        /// </summary>
        private static readonly TokenRequestContext RequestContext = new(["https://storage.azure.com/.default"]);

        /// <summary>
        /// The substituted provider the credential wraps.
        /// </summary>
        private readonly IAccessTokenProvider tokenProvider = Substitute.For<IAccessTokenProvider>();

        /// <summary>
        /// The credential under test.
        /// </summary>
        private readonly WorkloadIdentityTokenCredential credential;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkloadIdentityTokenCredentialTests"/> class.
        /// </summary>
        public WorkloadIdentityTokenCredentialTests()
        {
            this.tokenProvider.GetAccessTokenAsync(Arg.Any<CancellationToken>()).Returns(new AccessToken("access-token", ExpiresOn));
            this.credential = new WorkloadIdentityTokenCredential(this.tokenProvider);
        }

        /// <summary>
        /// Verifies that the asynchronous overload returns the provider's token with its real expiry.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_Provider_When_GetTokenAsync_Then_ReturnsProvidersTokenWithItsExpiry()
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();

            // Act
            var result = await this.credential.GetTokenAsync(RequestContext, cancellation.Token);

            // Assert
            result.Token.ShouldBe("access-token");
            result.ExpiresOn.ShouldBe(ExpiresOn);
            await this.tokenProvider.Received(1).GetAccessTokenAsync(cancellation.Token);
        }

        /// <summary>
        /// Verifies that the synchronous overload returns the provider's token with its real expiry.
        /// </summary>
        [Fact]
        public void Given_Provider_When_GetToken_Then_ReturnsProvidersTokenWithItsExpiry()
        {
            // Act
            var result = this.credential.GetToken(RequestContext, CancellationToken.None);

            // Assert
            result.Token.ShouldBe("access-token");
            result.ExpiresOn.ShouldBe(ExpiresOn);
        }

        /// <summary>
        /// Verifies that the credential cannot be constructed without a provider.
        /// </summary>
        [Fact]
        public void Given_NullProvider_When_Constructed_Then_ThrowsArgumentNullException()
        {
            // Act
            var exception = Should.Throw<ArgumentNullException>(() => new WorkloadIdentityTokenCredential(null!));

            // Assert
            exception.ParamName.ShouldBe("tokenProvider");
        }
    }
}
