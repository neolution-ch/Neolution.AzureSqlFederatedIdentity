namespace Csag.WorkloadIdentity.Options
{
    /// <summary>
    /// Options for configuring Azure SQL federated identity integration.
    /// </summary>
    public class AzureSqlFederatedIdentityOptions
    {
        /// <summary>
        /// Gets the configuration section the options are bound from by default.
        /// </summary>
        public static string ConfigurationSectionName { get; } = "Csag.WorkloadIdentity";

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

        /// <summary>
        /// Gets or sets how long before an access token expires it is treated as due for refresh. A call that finds
        /// the current token inside this window exchanges a fresh one, and the background refresh service refreshes
        /// the token this long before it expires. Must be positive; defaults to five minutes.
        /// </summary>
        public TimeSpan RefreshAheadWindow { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets a value indicating whether a hosted service keeps the access token refreshed ahead of its
        /// expiry, so that callers are served from the held token instead of waiting for a token exchange.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool EnableBackgroundRefresh { get; set; } = true;
    }
}
