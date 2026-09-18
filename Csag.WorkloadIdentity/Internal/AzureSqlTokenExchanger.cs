namespace Csag.WorkloadIdentity.Internal
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Abstractions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Exchanges a Google-signed ID token for an Azure AD access token for Azure SQL.
    /// </summary>
    internal class AzureSqlTokenExchanger : IAzureSqlTokenExchanger
    {
        /// <summary>
        /// The scope that grants access to Azure SQL.
        /// </summary>
        private const string AzureSqlScope = "https://database.windows.net/.default";

        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<AzureSqlTokenExchanger> logger;

        /// <summary>
        /// Provides Google-signed ID tokens for use as client assertions.
        /// </summary>
        private readonly IGoogleIdTokenProvider googleIdTokenProvider;

        /// <summary>
        /// Creates the credential that performs the exchange.
        /// </summary>
        private readonly IClientAssertionCredentialFactory credentialFactory;

        /// <summary>
        /// Guards <see cref="credential"/>.
        /// </summary>
        private readonly object credentialLock = new();

        /// <summary>
        /// The credential for the most recently requested tenant and client. The credential caches the tokens it
        /// obtains, so it is kept for as long as the same tenant and client are requested.
        /// </summary>
        private CachedCredential? credential;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSqlTokenExchanger"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="googleIdTokenProvider">The Google ID token provider.</param>
        /// <param name="credentialFactory">Creates the credential that performs the exchange.</param>
        public AzureSqlTokenExchanger(ILogger<AzureSqlTokenExchanger> logger, IGoogleIdTokenProvider googleIdTokenProvider, IClientAssertionCredentialFactory credentialFactory)
        {
            this.logger = logger;
            this.googleIdTokenProvider = googleIdTokenProvider;
            this.credentialFactory = credentialFactory;
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
            var tokenCredential = this.GetOrCreateCredential(tenantId, clientId);

            this.logger.LogTrace("Exchanging Google-signed ID token for Azure AD access token for Azure SQL using client assertion");
            var tokenRequestContext = new TokenRequestContext([AzureSqlScope]);
            var token = await tokenCredential.GetTokenAsync(tokenRequestContext, cancellationToken).ConfigureAwait(false);

            this.logger.LogDebug("Successfully obtained Azure AD access token expiring at {ExpiresOn}", token.ExpiresOn);
            return token;
        }

        /// <summary>
        /// Returns the credential for the given tenant and client, creating it on first use or when the pair changes.
        /// </summary>
        /// <param name="tenantId">The Azure AD tenant ID.</param>
        /// <param name="clientId">The Azure AD client ID.</param>
        /// <returns>The credential.</returns>
        private TokenCredential GetOrCreateCredential(string tenantId, string clientId)
        {
            lock (this.credentialLock)
            {
                var current = this.credential;
                if (current is null || current.TenantId != tenantId || current.ClientId != clientId)
                {
                    // The credential passes the callback the cancellation token of the token request it is serving.
                    var tokenCredential = this.credentialFactory.Create(tenantId, clientId, this.googleIdTokenProvider.GetIdTokenAsync);
                    current = new CachedCredential(tenantId, clientId, tokenCredential);
                    this.credential = current;
                }

                return current.Credential;
            }
        }

        /// <summary>
        /// A credential together with the tenant and client it was created for.
        /// </summary>
        /// <param name="TenantId">The Azure AD tenant ID.</param>
        /// <param name="ClientId">The Azure AD client ID.</param>
        /// <param name="Credential">The credential.</param>
        private sealed record CachedCredential(string TenantId, string ClientId, TokenCredential Credential);
    }
}
