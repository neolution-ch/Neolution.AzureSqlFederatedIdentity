namespace Csag.WorkloadIdentity.Internal
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Obtains access tokens from the managed identity endpoint of the Azure resource the application runs on.
    /// </summary>
    internal sealed class ManagedIdentityTokenExchanger : IWorkloadIdentityTokenExchanger
    {
        /// <summary>
        /// The options naming the managed identity of each resource.
        /// </summary>
        private readonly WorkloadIdentityOptions options;

        /// <summary>
        /// Creates the credential that requests the tokens.
        /// </summary>
        private readonly IManagedIdentityCredentialFactory credentialFactory;

        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<ManagedIdentityTokenExchanger> logger;

        /// <summary>
        /// Guards <see cref="credentials"/>.
        /// </summary>
        private readonly object credentialsLock = new();

        /// <summary>
        /// One credential per managed identity. A credential caches the tokens it obtains, so resources that share an
        /// identity share its credential.
        /// </summary>
        private readonly Dictionary<ManagedIdentityKey, TokenCredential> credentials = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedIdentityTokenExchanger"/> class.
        /// </summary>
        /// <param name="options">The workload identity options.</param>
        /// <param name="credentialFactory">Creates the credential that requests the tokens.</param>
        /// <param name="logger">The logger instance.</param>
        public ManagedIdentityTokenExchanger(
            IOptions<WorkloadIdentityOptions> options,
            IManagedIdentityCredentialFactory credentialFactory,
            ILogger<ManagedIdentityTokenExchanger> logger)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.options = options.Value;
            this.credentialFactory = credentialFactory;
            this.logger = logger;
        }

        /// <inheritdoc />
        public WorkloadIdentityProvider Provider => WorkloadIdentityProvider.ManagedIdentity;

        /// <inheritdoc />
        public async Task<AccessToken> ExchangeAsync(TokenScope scope, CancellationToken cancellationToken)
        {
            var resource = this.options.GetRequiredResource(scope);
            var managedIdentity = resource.ManagedIdentity ?? throw new InvalidOperationException($"The {scope} resource has no {nameof(resource.ManagedIdentity)} section.");
            var credential = this.GetOrCreateCredential(managedIdentity);

            this.logger.LogTrace("Requesting a Microsoft Entra ID access token for {Scope} from the managed identity endpoint.", scope);
            var tokenRequestContext = new TokenRequestContext([scope.GetIdentifier()]);
            var token = await credential.GetTokenAsync(tokenRequestContext, cancellationToken).ConfigureAwait(false);

            this.logger.LogDebug("Obtained a Microsoft Entra ID access token for {Scope} that expires at {ExpiresOn}.", scope, token.ExpiresOn);
            return token;
        }

        /// <summary>
        /// Returns the credential for the managed identity, creating it on first use.
        /// </summary>
        /// <param name="managedIdentity">The managed identity.</param>
        /// <returns>The credential.</returns>
        private TokenCredential GetOrCreateCredential(ManagedIdentityOptions managedIdentity)
        {
            // A client ID set alongside UseSystemAssignedIdentity is ignored, so it must not tell the identities apart.
            var key = new ManagedIdentityKey(managedIdentity.UseSystemAssignedIdentity, managedIdentity.UseSystemAssignedIdentity ? null : managedIdentity.ClientId);
            lock (this.credentialsLock)
            {
                if (!this.credentials.TryGetValue(key, out var credential))
                {
                    credential = this.credentialFactory.Create(managedIdentity);
                    this.credentials.Add(key, credential);
                }

                return credential;
            }
        }

        /// <summary>
        /// Identifies a managed identity by value.
        /// </summary>
        /// <param name="SystemAssigned">Whether the identity is the system-assigned one.</param>
        /// <param name="ClientId">The client ID of the user-assigned identity, or <see langword="null"/> for the system-assigned one.</param>
        private sealed record ManagedIdentityKey(bool SystemAssigned, string? ClientId);
    }
}
