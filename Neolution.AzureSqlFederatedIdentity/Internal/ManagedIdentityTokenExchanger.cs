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
    /// Exchanges a managed identity credential for an Azure AD access token for Azure SQL.
    /// </summary>
    internal class ManagedIdentityTokenExchanger : ITokenExchanger
    {
        /// <summary>
        /// Logger instance for logging messages related to the ManagedIdentityTokenExchanger.
        /// </summary>
        private readonly ILogger<ManagedIdentityTokenExchanger> logger;

        /// <summary>
        /// The managed identity credential used to obtain Azure AD access tokens.
        /// </summary>
        private readonly ManagedIdentityCredential credential;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedIdentityTokenExchanger"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging.</param>
        /// <param name="options">The managed identity options containing the client ID.</param>
        public ManagedIdentityTokenExchanger(ILogger<ManagedIdentityTokenExchanger> logger, ManagedIdentityOptions options)
        {
            this.logger = logger;

            // Initialize credential using user-assigned identity if ClientId specified
            this.credential = string.IsNullOrWhiteSpace(options.ClientId)
                ? new ManagedIdentityCredential()
                : new ManagedIdentityCredential(options.ClientId);
        }

        /// <inheritdoc />
        public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken)
        {
            this.logger.LogTrace("Getting Azure AD access token for Azure SQL using Managed Identity");
            var requestContext = new TokenRequestContext(new[] { "https://database.windows.net/.default" });
            var token = await this.credential.GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);
            this.logger.LogDebug("Obtained Azure AD access token via Managed Identity, length: {Length}", token.Token.Length);
            return token;
        }
    }
}
