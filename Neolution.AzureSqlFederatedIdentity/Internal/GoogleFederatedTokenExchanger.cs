namespace Neolution.AzureSqlFederatedIdentity.Internal
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Identity;
    using Microsoft.Extensions.Logging;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Options;

    /// <summary>
    /// Exchanges a Google-signed ID token for an Azure AD access token for Azure SQL.
    /// </summary>
    internal class GoogleFederatedTokenExchanger : ITokenExchanger
    {
        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<GoogleFederatedTokenExchanger> logger;

        /// <summary>
        /// Provides Google-signed ID tokens for client assertions.
        /// </summary>
        private readonly IGoogleIdTokenProvider googleIdTokenProvider;

        /// <summary>
        /// The federated identity options.
        /// </summary>
        private readonly FederatedIdentityOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleFederatedTokenExchanger"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for this exchanger.</param>
        /// <param name="googleIdTokenProvider">The provider for Google ID tokens.</param>
        /// <param name="options">The federated identity configuration options.</param>
        public GoogleFederatedTokenExchanger(ILogger<GoogleFederatedTokenExchanger> logger, IGoogleIdTokenProvider googleIdTokenProvider, FederatedIdentityOptions options)
        {
            this.logger = logger;
            this.googleIdTokenProvider = googleIdTokenProvider;
            this.options = options;
        }

        /// <inheritdoc />
        public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken)
        {
            var credential = new ClientAssertionCredential(
                this.options.TenantId,
                this.options.ClientId,
                async _ => await this.googleIdTokenProvider.GetIdTokenAsync(cancellationToken).ConfigureAwait(false));

            this.logger.LogTrace("Exchanging Google-signed ID token for Azure AD access token for Azure SQL using client assertion");
            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net/.default" });
            var token = await credential.GetTokenAsync(tokenRequestContext, cancellationToken).ConfigureAwait(false);

            this.logger.LogDebug("Successfully obtained Azure AD access token, length {Length}", token.Token.Length);
            return token;
        }
    }
}
