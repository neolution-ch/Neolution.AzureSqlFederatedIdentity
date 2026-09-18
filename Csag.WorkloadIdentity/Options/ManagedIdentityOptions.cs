namespace Csag.WorkloadIdentity.Options
{
    /// <summary>
    /// Names the Azure managed identity an access token is obtained with.
    /// </summary>
    public class ManagedIdentityOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the system-assigned managed identity of the hosting resource is
        /// used. When <see langword="true"/>, <see cref="ClientId"/> is ignored; when <see langword="false"/>,
        /// <see cref="ClientId"/> must name a user-assigned managed identity. Defaults to <see langword="false"/>.
        /// </summary>
        public bool UseSystemAssignedIdentity { get; set; }

        /// <summary>
        /// Gets or sets the client ID of the user-assigned managed identity. Required unless
        /// <see cref="UseSystemAssignedIdentity"/> is <see langword="true"/>.
        /// </summary>
        public string? ClientId { get; set; }
    }
}
