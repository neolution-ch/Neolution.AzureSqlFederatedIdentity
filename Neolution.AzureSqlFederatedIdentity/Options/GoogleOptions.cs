namespace Neolution.AzureSqlFederatedIdentity.Options
{
    /// <summary>
    /// Google-specific options for federated identity.
    /// </summary>
    public class GoogleOptions
    {
        /// <summary>
        /// Gets or sets the Google service account email to impersonate for ID token generation.
        /// </summary>
        public string? ServiceAccountEmail { get; set; }
    }
}
