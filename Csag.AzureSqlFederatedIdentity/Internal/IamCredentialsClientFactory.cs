namespace Csag.AzureSqlFederatedIdentity.Internal
{
    using Google.Cloud.Iam.Credentials.V1;

    /// <summary>
    /// Creates <see cref="IAMCredentialsClient"/> instances from the application default credentials.
    /// </summary>
    internal sealed class IamCredentialsClientFactory : IIamCredentialsClientFactory
    {
        /// <inheritdoc />
        public Task<IAMCredentialsClient> CreateAsync(CancellationToken cancellationToken)
        {
            return IAMCredentialsClient.CreateAsync(cancellationToken);
        }
    }
}
