namespace Neolution.AzureSqlFederatedIdentity.Internal.Exchangers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Identity;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Options;

    /// <summary>
    /// Exchanges a Google-signed ID token for an Azure AD access token for Azure SQL.
    /// </summary>
    internal class GoogleFederatedTokenExchanger : IWorkloadIdentityTokenExchanger
    {
        /// <summary>
        /// Provides logging functionality for the <see cref="GoogleFederatedTokenExchanger"/> class.
        /// </summary>
        private readonly ILogger<GoogleFederatedTokenExchanger> logger;

        /// <summary>
        /// Provides functionality to retrieve Google-signed ID tokens.
        /// </summary>
        private readonly GoogleIdTokenProvider googleIdTokenProvider;

        /// <summary>
        /// Provides access to Google configuration options.
        /// </summary>
        private readonly IOptionsMonitor<GoogleOptions> googleOptionsMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleFederatedTokenExchanger"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging.</param>
        /// <param name="googleIdTokenProvider">The provider for retrieving Google-signed ID tokens.</param>
        /// <param name="googleOptionsMonitor">The options monitor for Google configuration.</param>
        public GoogleFederatedTokenExchanger(
            ILogger<GoogleFederatedTokenExchanger> logger,
            GoogleIdTokenProvider googleIdTokenProvider,
            IOptionsMonitor<GoogleOptions> googleOptionsMonitor)
        {
            this.logger = logger;
            this.googleIdTokenProvider = googleIdTokenProvider;
            this.googleOptionsMonitor = googleOptionsMonitor;
        }

        /// <summary>
        /// Retrieves an Azure AD access token for the specified logical context using a Google-signed ID token.
        /// </summary>
        /// <param name="scope">The logical context for which the access token is requested.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An <see cref="AccessToken" /> containing the Azure AD access token.</returns>
        public async Task<AccessToken> GetTokenAsync(AzureTokenScope scope, CancellationToken cancellationToken)
        {
            var options = this.googleOptionsMonitor.Get(scope.GetOptionsName());
            var credential = new ClientAssertionCredential(
                options.TenantId,
                options.ClientId,
                async _ => await this.googleIdTokenProvider.GetIdTokenAsync(options.ServiceAccountEmail, cancellationToken).ConfigureAwait(false));

            this.logger.LogTrace("Exchanging Google-signed ID token for Azure AD access token for {Identifier} using client assertion", scope);
            var tokenRequestContext = new TokenRequestContext(new[] { scope.GetIdentifier() });
            var token = await credential.GetTokenAsync(tokenRequestContext, cancellationToken).ConfigureAwait(false);

            this.logger.LogDebug("Successfully obtained Azure AD access token, length {Length}", token.Token.Length);
            return token;
        }
    }
}
