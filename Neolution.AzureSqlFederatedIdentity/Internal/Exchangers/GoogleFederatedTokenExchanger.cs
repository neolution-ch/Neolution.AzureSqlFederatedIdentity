namespace Neolution.AzureSqlFederatedIdentity.Internal.Exchangers
{
    using System;
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
        private readonly ILogger<GoogleFederatedTokenExchanger> logger;
        private readonly GoogleIdTokenProvider googleIdTokenProvider;
        private readonly IOptionsMonitor<GoogleOptions> googleOptionsMonitor;

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
        /// <param name="context">The logical context for which the access token is requested.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An <see cref="AccessToken" /> containing the Azure AD access token.</returns>
        public async Task<AccessToken> GetTokenAsync(TokenContext context, CancellationToken cancellationToken)
        {
            if (!TokenContextMappings.Map.TryGetValue(context, out var mapping))
            {
                throw new ArgumentException($"Unknown TokenContext: {context}", nameof(context));
            }
            var options = this.googleOptionsMonitor.Get(mapping.OptionsName);
            var credential = new ClientAssertionCredential(
                options.TenantId,
                options.ClientId,
                async _ => await this.googleIdTokenProvider.GetIdTokenAsync(options.ServiceAccountEmail, cancellationToken).ConfigureAwait(false));

            this.logger.LogTrace("Exchanging Google-signed ID token for Azure AD access token for {Context} using client assertion", context);
            var tokenRequestContext = new TokenRequestContext(new[] { mapping.Scope });
            var token = await credential.GetTokenAsync(tokenRequestContext, cancellationToken).ConfigureAwait(false);

            this.logger.LogDebug("Successfully obtained Azure AD access token, length {Length}", token.Token.Length);
            return token;
        }
    }
}
