namespace Csag.WorkloadIdentity.Options
{
    /// <summary>
    /// Configures how access tokens for one Azure resource are obtained: which identity provider is used and the
    /// settings of that provider.
    /// </summary>
    public class WorkloadIdentityResourceOptions
    {
        /// <summary>
        /// Gets or sets the identity provider the access token is obtained with. Defaults to
        /// <see cref="WorkloadIdentityProvider.ManagedIdentity"/>.
        /// </summary>
        public WorkloadIdentityProvider Provider { get; set; }

        /// <summary>
        /// Gets or sets the managed identity settings. Required when <see cref="Provider"/> is
        /// <see cref="WorkloadIdentityProvider.ManagedIdentity"/>; ignored otherwise.
        /// </summary>
        public ManagedIdentityOptions? ManagedIdentity { get; set; }

        /// <summary>
        /// Gets or sets the Google federation settings. Required when <see cref="Provider"/> is
        /// <see cref="WorkloadIdentityProvider.Google"/>; ignored otherwise.
        /// </summary>
        public GoogleOptions? Google { get; set; }
    }
}
