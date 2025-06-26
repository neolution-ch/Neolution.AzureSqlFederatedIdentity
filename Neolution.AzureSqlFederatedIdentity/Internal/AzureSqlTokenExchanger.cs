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
    public class AzureSqlTokenExchanger
    {
        /// <summary>
        /// Provides logging functionality for the <see cref="AzureSqlTokenExchanger"/> class.
        /// </summary>
        private readonly ILogger<AzureSqlTokenExchanger> logger;

        /// <summary>
        /// Provides the interface for obtaining Google ID tokens.
        /// </summary>
        private readonly GoogleIdTokenProvider googleIdTokenProvider;

        /// <summary>
        /// Provides Google options containing TenantId, ClientId, and ServiceAccountEmail.
        /// </summary>
        private readonly GoogleOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSqlTokenExchanger"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging.</param>
        /// <param name="googleIdTokenProvider">The provider for obtaining Google ID tokens.</param>
        /// <param name="options">The Google options containing TenantId and ClientId.</param>
        public AzureSqlTokenExchanger(
            ILogger<AzureSqlTokenExchanger> logger,
            GoogleIdTokenProvider googleIdTokenProvider,
            GoogleOptions options)
        {
            this.logger = logger;
            this.googleIdTokenProvider = googleIdTokenProvider;
            this.options = options;
        }

        /// <summary>
        /// Gets an Azure AD access token for Azure SQL using a Google-signed ID token.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The Azure AD access token.</returns>
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
