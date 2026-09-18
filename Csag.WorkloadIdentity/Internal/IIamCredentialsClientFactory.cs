namespace Csag.WorkloadIdentity.Internal
{
    using Google.Cloud.Iam.Credentials.V1;

    /// <summary>
    /// Creates the Google IAM Credentials client used to mint service account ID tokens. Exists so that the ID token
    /// provider can be tested without Google application default credentials.
    /// </summary>
    internal interface IIamCredentialsClientFactory
    {
        /// <summary>
        /// Creates a client authenticated with the application default credentials.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The client.</returns>
        Task<IAMCredentialsClient> CreateAsync(CancellationToken cancellationToken);
    }
}
