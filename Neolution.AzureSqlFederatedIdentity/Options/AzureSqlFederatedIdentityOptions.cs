namespace Neolution.AzureSqlFederatedIdentity.Options
{
    /// <summary>
    /// Options for configuring Azure SQL federated identity integration.
    /// </summary>
    public class AzureSqlFederatedIdentityOptions
    {
        /// <summary>
        /// Gets or sets the Azure AD tenant ID.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Azure AD client ID.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Google-specific options.
        /// </summary>
        public GoogleOptions? Google { get; set; }
    }
}
