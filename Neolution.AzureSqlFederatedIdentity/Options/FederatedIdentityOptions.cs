namespace Neolution.AzureSqlFederatedIdentity.Options
{
    /// <summary>
    /// Options for federated identity (Google service account) authentication.
    /// </summary>
    public class FederatedIdentityOptions
    {
        /// <summary>
        /// Gets or sets the Azure AD tenant ID.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Azure AD application (client) ID.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets Google-specific options for federated identity.
        /// </summary>
        public GoogleOptions Google { get; set; } = new GoogleOptions();
    }
}
