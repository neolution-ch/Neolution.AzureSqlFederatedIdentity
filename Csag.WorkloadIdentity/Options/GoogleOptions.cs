namespace Csag.WorkloadIdentity.Options
{
    /// <summary>
    /// Settings for obtaining an access token through Google workload identity federation: the application's Google
    /// identity mints an ID token for a service account, and Microsoft Entra ID accepts that ID token as the client
    /// assertion of the identity that holds the federated credential.
    /// </summary>
    public class GoogleOptions
    {
        /// <summary>
        /// Gets or sets the directory (tenant) ID of the Microsoft Entra tenant that issues the access token.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets the client ID of the Microsoft Entra identity that holds the federated credential trusting
        /// the service account: either the application (client) ID of an app registration or the client ID of a
        /// user-assigned managed identity.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the email of the Google service account the ID token is minted for. The federated credential
        /// names this account's unique ID as its subject.
        /// </summary>
        public string? ServiceAccountEmail { get; set; }
    }
}
