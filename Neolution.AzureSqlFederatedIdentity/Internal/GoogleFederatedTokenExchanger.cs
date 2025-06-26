namespace Neolution.AzureSqlFederatedIdentity.Internal
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Identity;
    using Microsoft.Extensions.Logging;
    using Neolution.AzureSqlFederatedIdentity.Options;

    /// <summary>
    /// Exchanges a Google-signed ID token for an Azure AD access token for Azure SQL.
    /// </summary>
    internal class GoogleFederatedTokenExchanger
    {
        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<GoogleFederatedTokenExchanger> logger;

        /// <summary>
        /// Provides Google-signed ID tokens for client assertions.
        /// </summary>
        private readonly GoogleIdTokenProvider googleIdTokenProvider;

        /// <summary>
        /// The federated identity options.
        /// </summary>
        private readonly GoogleOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleFederatedTokenExchanger"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for this exchanger.</param>
        /// <param name="googleIdTokenProvider">The provider for Google ID tokens.</param>
        /// <param name="options">The federated identity configuration options.</param>
        public GoogleFederatedTokenExchanger(ILogger<GoogleFederatedTokenExchanger> logger, GoogleIdTokenProvider googleIdTokenProvider, GoogleOptions options)
        {
            this.logger = logger;
            this.googleIdTokenProvider = googleIdTokenProvider;
            this.options = options;
        }

        /// <summary>
        /// Retrieves an Azure AD access token for Azure SQL using a Google-signed ID token.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An <see cref="AccessToken"/> containing the Azure AD access token.</returns>
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
