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
    /// Exchanges a managed identity credential for an Azure AD access token for Azure SQL.
    /// </summary>
    internal class ManagedIdentityTokenExchanger : IWorkloadIdentityTokenExchanger
    {
        private readonly ILogger<ManagedIdentityTokenExchanger> logger;
        private readonly IOptionsMonitor<ManagedIdentityOptions> managedIdentityOptionsMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedIdentityTokenExchanger"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging.</param>
        /// <param name="managedIdentityOptionsMonitor">The managed identity options monitor.</param>
        public ManagedIdentityTokenExchanger(
            ILogger<ManagedIdentityTokenExchanger> logger,
            IOptionsMonitor<ManagedIdentityOptions> managedIdentityOptionsMonitor)
        {
            this.logger = logger;
            this.managedIdentityOptionsMonitor = managedIdentityOptionsMonitor;
        }

        /// <summary>
        /// Retrieves an Azure AD access token for the specified logical context using the managed identity credential.
        /// </summary>
        /// <param name="context">The logical context for which the access token is requested.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An <see cref="AccessToken" /> containing the token and its expiration information.</returns>
        public async Task<AccessToken> GetTokenAsync(TokenContext context, CancellationToken cancellationToken)
        {
            if (!TokenContextMappings.Map.TryGetValue(context, out var mapping))
            {
                throw new ArgumentException($"Unknown TokenContext: {context}", nameof(context));
            }
            var options = this.managedIdentityOptionsMonitor.Get(mapping.OptionsName);
            this.logger.LogTrace("Getting Azure AD access token for {Context} using Managed Identity", context);
            var requestContext = new TokenRequestContext(new[] { mapping.Scope });
            var credential = string.IsNullOrWhiteSpace(options.ClientId)
                ? new ManagedIdentityCredential()
                : new ManagedIdentityCredential(options.ClientId);
            var token = await credential.GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);
            this.logger.LogDebug("Obtained Azure AD access token via Managed Identity, length: {Length}", token.Token.Length);
            return token;
        }
    }
}
