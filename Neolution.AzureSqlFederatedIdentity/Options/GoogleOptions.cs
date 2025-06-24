namespace Neolution.AzureSqlFederatedIdentity.Options
{
    /// <summary>
    /// Google-specific options for federated identity.
    /// </summary>
    public class GoogleOptions
    {
        /// <summary>
        /// Gets or sets the Azure AD application (client) ID used for token requests.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Azure AD tenant ID.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Google service account email used to request ID tokens.
        /// </summary>
        public string ServiceAccountEmail { get; set; } = string.Empty;
    }
}
