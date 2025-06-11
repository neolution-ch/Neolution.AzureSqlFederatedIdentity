namespace Neolution.AzureSqlFederatedIdentity.Internal
{
    using Azure.Core;
    using Azure.Identity;
    using Microsoft.Extensions.Logging;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;

    /// <summary>
    /// Exchanges a Google-signed ID token for an Azure AD access token for Azure SQL.
    /// </summary>
    internal class AzureSqlTokenExchanger : IAzureSqlTokenExchanger
    {
        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<AzureSqlTokenExchanger> logger;

        /// <summary>
        /// Provides Google-signed ID tokens for use as client assertions.
        /// </summary>
        private readonly IGoogleIdTokenProvider googleIdTokenProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSqlTokenExchanger"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="googleIdTokenProvider">The Google ID token provider.</param>
        public AzureSqlTokenExchanger(ILogger<AzureSqlTokenExchanger> logger, IGoogleIdTokenProvider googleIdTokenProvider)
        {
            this.logger = logger;
            this.googleIdTokenProvider = googleIdTokenProvider;
        }

        /// <summary>
        /// Exchanges a Google-signed ID token for an Azure AD access token for Azure SQL.
        /// </summary>
        /// <param name="tenantId">The Azure AD tenant ID.</param>
        /// <param name="clientId">The Azure AD client ID.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An Azure AD access token for Azure SQL.</returns>
        public async Task<AccessToken> ExchangeClientAssertionForAzureTokenAsync(string tenantId, string clientId, CancellationToken cancellationToken)
        {
            var credential = new ClientAssertionCredential(
                tenantId,
                clientId,
                async _ => await this.googleIdTokenProvider.GetIdTokenAsync(cancellationToken).ConfigureAwait(false));

            this.logger.LogTrace("Exchanging Google-signed ID token for Azure AD access token for Azure SQL using client assertion");
            var tokenRequestContext = new TokenRequestContext(["https://database.windows.net/.default"]);
            var token = await credential.GetTokenAsync(tokenRequestContext, cancellationToken).ConfigureAwait(false);

            this.logger.LogDebug("Successfully obtained Azure AD access token, length {Length}", token.Token.Length);
            return token;
        }
    }
}
