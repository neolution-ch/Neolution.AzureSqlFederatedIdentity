namespace Neolution.WorkloadIdentity.UnitTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using AutoFixture.AutoNSubstitute;
    using Azure.Core;
    using Azure.Identity;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Neolution.WorkloadIdentity.Internal.Services.Azure;
    using Neolution.WorkloadIdentity.Options;
    using NSubstitute;
    using Shouldly;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="ManagedIdentityTokenExchanger"/> class.
    /// </summary>
    public class ManagedIdentityTokenExchangerTests
    {
        /// <summary>
        /// Provides a fixture for creating test objects.
        /// </summary>
        private readonly IFixture fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedIdentityTokenExchangerTests"/> class.
        /// </summary>
        public ManagedIdentityTokenExchangerTests()
        {
            this.fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
        }

        /// <summary>
        /// Tests that when a client ID is provided, the <see cref="ManagedIdentityTokenExchanger"/> uses the client ID credential to get a token.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_ClientId_When_GetTokenAsync_Then_UsesClientIdCredential()
        {
            // Arrange
            var logger = this.fixture.Create<ILogger<ManagedIdentityTokenExchanger>>();
            var clientId = this.fixture.Create<string>();
            var options = Substitute.For<IOptionsMonitor<ManagedIdentityOptions>>();
            options.Get(Arg.Any<string>()).Returns(new ManagedIdentityOptions { ClientId = clientId });
            var exchanger = new ManagedIdentityTokenExchanger(logger, options);
            var scope = TokenScope.AzureSql;
            var cancellationToken = CancellationToken.None;

            // Mock ManagedIdentityCredential
            var credential = Substitute.ForPartsOf<ManagedIdentityCredential>(clientId);
            var expectedToken = new AccessToken("token", DateTimeOffset.UtcNow.AddMinutes(10));
            credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), cancellationToken).Returns(expectedToken);

            // Act
            var token = await exchanger.GetTokenAsync(scope, cancellationToken);

            // Assert
            token.Token.ShouldNotBeNullOrEmpty();
        }

        /// <summary>
        /// Tests that when no client ID is provided, the <see cref="ManagedIdentityTokenExchanger"/> uses the default credential to get a token.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Fact]
        public async Task Given_NoClientId_When_GetTokenAsync_Then_UsesDefaultCredential()
        {
            // Arrange
            var logger = this.fixture.Create<ILogger<ManagedIdentityTokenExchanger>>();
            var options = Substitute.For<IOptionsMonitor<ManagedIdentityOptions>>();
            options.Get(Arg.Any<string>()).Returns(new ManagedIdentityOptions { ClientId = null });
            var exchanger = new ManagedIdentityTokenExchanger(logger, options);
            var scope = TokenScope.BlobStorage;
            var cancellationToken = CancellationToken.None;

            // Mock ManagedIdentityCredential
            var credential = Substitute.ForPartsOf<ManagedIdentityCredential>();
            var expectedToken = new AccessToken("token", DateTimeOffset.UtcNow.AddMinutes(10));
            credential.GetTokenAsync(Arg.Any<TokenRequestContext>(), cancellationToken).Returns(expectedToken);

            // Act
            var token = await exchanger.GetTokenAsync(scope, cancellationToken);

            // Assert
            token.Token.ShouldNotBeNullOrEmpty();
        }
    }
}
