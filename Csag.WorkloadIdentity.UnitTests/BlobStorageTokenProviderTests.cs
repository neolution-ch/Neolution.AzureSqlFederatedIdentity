namespace Csag.WorkloadIdentity.UnitTests
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Internal;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Time.Testing;
    using NSubstitute;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="BlobStorageTokenProvider"/> class, which wraps a <see cref="ResourceTokenProvider"/>.
    /// </summary>
    public class BlobStorageTokenProviderTests
    {
        /// <summary>
        /// The instant at which every test starts.
        /// </summary>
        private static readonly DateTimeOffset StartTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// The substituted token exchanger.
        /// </summary>
        private readonly IWorkloadIdentityTokenExchanger exchanger = Substitute.For<IWorkloadIdentityTokenExchanger>();

        /// <summary>
        /// Verifies that the string overload returns the token value obtained for Blob Storage.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_Token_When_GetBlobStorageAccessTokenAsync_Then_ReturnsTokenValue()
        {
            // Arrange
            this.SetupExchange(new AccessToken("blob-token", StartTime.AddHours(1)));
            using var provider = this.CreateProvider();

            // Act
            var result = await provider.GetBlobStorageAccessTokenAsync(CancellationToken.None);

            // Assert
            result.ShouldBe("blob-token");
            await this.exchanger.Received(1).ExchangeAsync(TokenScope.BlobStorage, Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that the token overload returns the token together with its real expiry.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_Token_When_GetAccessTokenAsync_Then_ReturnsTokenWithExpiry()
        {
            // Arrange
            var expiresOn = StartTime.AddHours(1);
            this.SetupExchange(new AccessToken("blob-token", expiresOn));
            using var provider = this.CreateProvider();

            // Act
            var result = await provider.GetAccessTokenAsync(CancellationToken.None);

            // Assert
            result.Token.ShouldBe("blob-token");
            result.ExpiresOn.ShouldBe(expiresOn);
        }

        /// <summary>
        /// Verifies that a forced refresh replaces the held token and reports the new expiry.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_HeldToken_When_RefreshAsync_Then_ReplacesItAndReturnsNewExpiry()
        {
            // Arrange
            var secondExpiry = StartTime.AddHours(2);
            this.SetupExchange(new AccessToken("first", StartTime.AddHours(1)), new AccessToken("second", secondExpiry));
            using var provider = this.CreateProvider();
            await provider.GetBlobStorageAccessTokenAsync(CancellationToken.None);

            // Act
            var expiresOn = await ((ITokenRefresher)provider).RefreshAsync(CancellationToken.None);
            var result = await provider.GetBlobStorageAccessTokenAsync(CancellationToken.None);

            // Assert
            expiresOn.ShouldBe(secondExpiry);
            result.ShouldBe("second");
            await this.exchanger.Received(2).ExchangeAsync(TokenScope.BlobStorage, Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Configures the exchanger to hand out the given tokens in order, repeating the last one.
        /// </summary>
        /// <param name="tokens">The tokens to hand out.</param>
        private void SetupExchange(params AccessToken[] tokens)
        {
            this.exchanger.ExchangeAsync(TokenScope.BlobStorage, Arg.Any<CancellationToken>())
                .Returns(tokens[0], tokens.Skip(1).ToArray());
        }

        /// <summary>
        /// Creates the provider under test.
        /// </summary>
        /// <returns>The provider.</returns>
        private BlobStorageTokenProvider CreateProvider()
        {
            var tokenProvider = new ResourceTokenProvider(TokenScope.BlobStorage, this.exchanger, TimeSpan.FromMinutes(5), new FakeTimeProvider(StartTime), NullLogger<ResourceTokenProvider>.Instance);
            return new BlobStorageTokenProvider(tokenProvider);
        }
    }
}
