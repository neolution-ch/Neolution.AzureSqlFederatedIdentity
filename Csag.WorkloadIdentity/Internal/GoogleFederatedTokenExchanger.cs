namespace Csag.WorkloadIdentity.Internal
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Abstractions;
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Obtains access tokens through Google workload identity federation: a Google-signed ID token for the resource's
    /// service account is presented to Microsoft Entra ID as the client assertion of the identity that holds the
    /// federated credential.
    /// </summary>
    internal sealed class GoogleFederatedTokenExchanger : IWorkloadIdentityTokenExchanger
    {
        /// <summary>
        /// The options naming the federated identity of each resource.
        /// </summary>
        private readonly WorkloadIdentityOptions options;

        /// <summary>
        /// Provides Google-signed ID tokens for use as client assertions.
        /// </summary>
        private readonly IGoogleIdTokenProvider googleIdTokenProvider;

        /// <summary>
        /// Creates the credential that performs the exchange.
        /// </summary>
        private readonly IClientAssertionCredentialFactory credentialFactory;

        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<GoogleFederatedTokenExchanger> logger;

        /// <summary>
        /// Guards <see cref="credentials"/>.
        /// </summary>
        private readonly object credentialsLock = new();

        /// <summary>
        /// One credential per federated identity. A credential caches the tokens it obtains, so resources that share
        /// an identity share its credential. The service account is part of the identity because the credential's
        /// assertion callback is bound to it.
        /// </summary>
        private readonly Dictionary<FederatedIdentity, TokenCredential> credentials = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleFederatedTokenExchanger"/> class.
        /// </summary>
        /// <param name="options">The workload identity options.</param>
        /// <param name="googleIdTokenProvider">The Google ID token provider.</param>
        /// <param name="credentialFactory">Creates the credential that performs the exchange.</param>
        /// <param name="logger">The logger instance.</param>
        public GoogleFederatedTokenExchanger(
            IOptions<WorkloadIdentityOptions> options,
            IGoogleIdTokenProvider googleIdTokenProvider,
            IClientAssertionCredentialFactory credentialFactory,
            ILogger<GoogleFederatedTokenExchanger> logger)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.options = options.Value;
            this.googleIdTokenProvider = googleIdTokenProvider;
            this.credentialFactory = credentialFactory;
            this.logger = logger;
        }

        /// <inheritdoc />
        public WorkloadIdentityProvider Provider => WorkloadIdentityProvider.Google;

        /// <inheritdoc />
        public async Task<AccessToken> ExchangeAsync(TokenScope scope, CancellationToken cancellationToken)
        {
            var identity = GetIdentity(scope, this.options.GetRequiredResource(scope));
            var credential = this.GetOrCreateCredential(identity);

            this.logger.LogTrace("Exchanging a Google-signed ID token for a Microsoft Entra ID access token for {Scope}.", scope);
            var tokenRequestContext = new TokenRequestContext([scope.GetIdentifier()]);
            var token = await credential.GetTokenAsync(tokenRequestContext, cancellationToken).ConfigureAwait(false);

            this.logger.LogDebug("Obtained a Microsoft Entra ID access token for {Scope} that expires at {ExpiresOn}.", scope, token.ExpiresOn);
            return token;
        }

        /// <summary>
        /// Reads the federated identity a resource is configured with.
        /// </summary>
        /// <param name="scope">The resource.</param>
        /// <param name="resource">The resource's options.</param>
        /// <returns>The federated identity.</returns>
        private static FederatedIdentity GetIdentity(TokenScope scope, WorkloadIdentityResourceOptions resource)
        {
            var google = resource.Google ?? throw new InvalidOperationException($"The {scope} resource has no {nameof(resource.Google)} section.");
            return new FederatedIdentity(
                Require(scope, nameof(google.TenantId), google.TenantId),
                Require(scope, nameof(google.ClientId), google.ClientId),
                Require(scope, nameof(google.ServiceAccountEmail), google.ServiceAccountEmail));
        }

        /// <summary>
        /// Returns a required value of the Google section, or throws when it is blank.
        /// </summary>
        /// <param name="scope">The resource.</param>
        /// <param name="key">The key of the value within the Google section.</param>
        /// <param name="value">The configured value, if any.</param>
        /// <returns>The value, which is not blank.</returns>
        private static string Require(TokenScope scope, string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{scope}:{nameof(WorkloadIdentityResourceOptions.Google)}:{key} must be provided.");
            }

            return value;
        }

        /// <summary>
        /// Returns the credential for the federated identity, creating it on first use.
        /// </summary>
        /// <param name="identity">The federated identity.</param>
        /// <returns>The credential.</returns>
        private TokenCredential GetOrCreateCredential(FederatedIdentity identity)
        {
            lock (this.credentialsLock)
            {
                if (!this.credentials.TryGetValue(identity, out var credential))
                {
                    // The credential passes the callback the cancellation token of the token request it is serving.
                    credential = this.credentialFactory.Create(
                        identity.TenantId,
                        identity.ClientId,
                        cancellationToken => this.googleIdTokenProvider.GetIdTokenAsync(identity.ServiceAccountEmail, cancellationToken));
                    this.credentials.Add(identity, credential);
                }

                return credential;
            }
        }

        /// <summary>
        /// The identity a Google-federated token is obtained for.
        /// </summary>
        /// <param name="TenantId">The Microsoft Entra tenant ID.</param>
        /// <param name="ClientId">The client ID of the identity holding the federated credential.</param>
        /// <param name="ServiceAccountEmail">The Google service account the ID token is minted for.</param>
        private sealed record FederatedIdentity(string TenantId, string ClientId, string ServiceAccountEmail);
    }
}
