namespace Csag.WorkloadIdentity.Options
{
    /// <summary>
    /// The identity an access token is obtained with.
    /// </summary>
    public enum WorkloadIdentityProvider
    {
        /// <summary>
        /// The Azure managed identity, system-assigned or user-assigned, of the resource the application runs on.
        /// </summary>
        ManagedIdentity = 0,

        /// <summary>
        /// The application's Google identity, exchanged for a Microsoft Entra ID token through workload identity
        /// federation.
        /// </summary>
        Google = 1,
    }
}
