namespace Neolution.AzureSqlFederatedIdentity.Internal
{
    using System.IdentityModel.Tokens.Jwt;
    using Google.Cloud.Iam.Credentials.V1;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Neolution.AzureSqlFederatedIdentity.Options;

    /// <summary>
    /// Provides Google-signed ID tokens for use as client assertions in Azure SQL token exchange.
    /// </summary>
    public class GoogleIdTokenProvider
    {
        /// <summary>
        /// The logger instance for this class.
        /// </summary>
        private readonly ILogger<GoogleIdTokenProvider> logger;

        /// <summary>
        /// The federated identity options.
        /// </summary>
        private readonly GoogleOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleIdTokenProvider"/> class.
        /// </summary>
        /// <param name="options">The federated identity options.</param>
        /// <param name="logger">The logger instance.</param>
        public GoogleIdTokenProvider(IOptions<AzureSqlOptions> options, ILogger<GoogleIdTokenProvider> logger)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.options = options.Value.Google;
            this.logger = logger;
        }

        /// <summary>
        /// Gets a Google-signed ID token for the configured service account and audience.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The Google-signed ID token.</returns>
        public async Task<string> GetIdTokenAsync(CancellationToken cancellationToken)
        {
            IAMCredentialsClient client;
            try
            {
                client = await IAMCredentialsClient.CreateAsync(cancellationToken).ConfigureAwait(false);
                this.logger.LogTrace("Created IAMCredentialsClient successfully.");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to create IAMCredentialsClient. Check that IAM Credentials API is enabled and ADC is set.");
                throw;
            }

            var serviceAccountEmail = this.options.ServiceAccountEmail;
            var name = $"projects/-/serviceAccounts/{serviceAccountEmail}";
            this.logger.LogTrace("Requesting ID token for service account {ServiceAccountEmail}", serviceAccountEmail);

            var request = new GenerateIdTokenRequest
            {
                Name = name,
                Audience = "api://AzureADTokenExchange",
                IncludeEmail = false,
            };

            GenerateIdTokenResponse response;
            try
            {
                response = await client.GenerateIdTokenAsync(request, cancellationToken).ConfigureAwait(false);
                this.logger.LogTrace("Successfully generated ID token for service account {ServiceAccountEmail}", serviceAccountEmail);
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

            var idToken = response.Token;
            if (string.IsNullOrWhiteSpace(idToken))
            {
                throw new InvalidOperationException($"ID token was not returned for service account {serviceAccountEmail}");
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(idToken))
                {
                    var token = handler.ReadJwtToken(idToken);
                    this.logger.LogDebug("JWT claims: iss={iss}, aud={aud}, sub={sub}, exp={exp}", token.Issuer, string.Join(",", token.Audiences), token.Subject, token.ValidTo);
                }
                else
                {
                    this.logger.LogWarning("Cannot read JWT format.");
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to decode JWT for inspection.");
            }

            return idToken;
        }
    }
}
