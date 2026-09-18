namespace Csag.AzureSqlFederatedIdentity.Internal
{
    using Csag.AzureSqlFederatedIdentity.Abstractions;
    using Csag.AzureSqlFederatedIdentity.Options;
    using Google.Cloud.Iam.Credentials.V1;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Provides Google-signed ID tokens for use as client assertions in Azure SQL token exchange.
    /// </summary>
    internal class GoogleIdTokenProvider : IGoogleIdTokenProvider
    {
        /// <summary>
        /// The audience Azure AD expects in an ID token presented as a client assertion for workload identity federation.
        /// </summary>
        private const string AzureAdTokenExchangeAudience = "api://AzureADTokenExchange";

        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<GoogleIdTokenProvider> logger;

        /// <summary>
        /// The Google-specific options.
        /// </summary>
        private readonly GoogleOptions options;

        /// <summary>
        /// Creates the IAM Credentials client.
        /// </summary>
        private readonly IIamCredentialsClientFactory clientFactory;

        /// <summary>
        /// Guards <see cref="clientCreation"/>.
        /// </summary>
        private readonly object clientLock = new();

        /// <summary>
        /// The pending or completed creation of the IAM Credentials client, shared by all callers so that the client
        /// and its gRPC channel are created once. A failed creation is discarded so that the next call retries it.
        /// </summary>
        private Task<IAMCredentialsClient>? clientCreation;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleIdTokenProvider"/> class.
        /// </summary>
        /// <param name="options">The federated identity options.</param>
        /// <param name="clientFactory">Creates the IAM Credentials client.</param>
        /// <param name="logger">The logger instance.</param>
        public GoogleIdTokenProvider(IOptions<AzureSqlFederatedIdentityOptions> options, IIamCredentialsClientFactory clientFactory, ILogger<GoogleIdTokenProvider> logger)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(options.Value.Google);

            this.options = options.Value.Google;
            this.clientFactory = clientFactory;
            this.logger = logger;
        }

        /// <summary>
        /// Gets a Google-signed ID token for the configured service account and the Azure AD token exchange audience.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The Google-signed ID token.</returns>
        public async Task<string> GetIdTokenAsync(CancellationToken cancellationToken)
        {
            var client = await this.GetClientAsync(cancellationToken).ConfigureAwait(false);

            var serviceAccountEmail = this.options.ServiceAccountEmail;
            this.logger.LogTrace("Requesting ID token for service account {ServiceAccountEmail}", serviceAccountEmail);

            var request = new GenerateIdTokenRequest
            {
                Name = $"projects/-/serviceAccounts/{serviceAccountEmail}",
                Audience = AzureAdTokenExchangeAudience,
                IncludeEmail = false,
            };

            GenerateIdTokenResponse response;
            try
            {
                response = await client.GenerateIdTokenAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (RpcException ex)
            {
                this.logger.LogError(ex, "RPC error when calling GenerateIdTokenAsync for service account {ServiceAccountEmail}", serviceAccountEmail);
                throw;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to generate ID token for service account {ServiceAccountEmail}", serviceAccountEmail);
                throw;
            }

            if (string.IsNullOrWhiteSpace(response.Token))
            {
                throw new InvalidOperationException($"ID token was not returned for service account {serviceAccountEmail}");
            }

            this.logger.LogTrace("Successfully generated ID token for service account {ServiceAccountEmail}", serviceAccountEmail);
            return response.Token;
        }

        /// <summary>
        /// Returns the shared IAM Credentials client, starting its creation on first use or after a failed attempt.
        /// The creation is shared by every caller, so it is not tied to any caller's cancellation token; each caller
        /// only stops waiting for it when its own token is cancelled.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The client.</returns>
        private Task<IAMCredentialsClient> GetClientAsync(CancellationToken cancellationToken)
        {
            Task<IAMCredentialsClient> creation;
            lock (this.clientLock)
            {
                var current = this.clientCreation;
                creation = current is null || current.IsFaulted ? this.StartClientCreationAsync() : current;
            }

            return creation.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Starts a new creation of the client and records it as the shared one. Must be called under <see cref="clientLock"/>.
        /// </summary>
        /// <returns>The pending creation.</returns>
        private Task<IAMCredentialsClient> StartClientCreationAsync()
        {
            var creation = this.CreateClientAsync(CancellationToken.None);
            this.clientCreation = creation;
            return creation;
        }

        /// <summary>
        /// Creates the IAM Credentials client from the application default credentials.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The client.</returns>
        private async Task<IAMCredentialsClient> CreateClientAsync(CancellationToken cancellationToken)
        {
            try
            {
                var client = await this.clientFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
                this.logger.LogTrace("Created IAMCredentialsClient successfully.");
                return client;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to create IAMCredentialsClient. Check that the IAM Service Account Credentials API is enabled and application default credentials are available.");
                throw;
            }
        }
    }
}
