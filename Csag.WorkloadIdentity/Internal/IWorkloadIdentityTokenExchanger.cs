namespace Csag.WorkloadIdentity.Internal
{
    using Azure.Core;
    using Csag.WorkloadIdentity.Options;

    /// <summary>
    /// Obtains an access token for a resource from Microsoft Entra ID with one kind of identity. The token provider
    /// of a resource uses the exchanger whose <see cref="Provider"/> the resource's options select.
    /// </summary>
    internal interface IWorkloadIdentityTokenExchanger
    {
        /// <summary>
        /// Gets the identity provider this exchanger obtains tokens with.
        /// </summary>
        WorkloadIdentityProvider Provider { get; }

        /// <summary>
        /// Obtains an access token for the resource, using the identity its options configure.
        /// </summary>
        /// <param name="scope">The resource.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token.</returns>
        Task<AccessToken> ExchangeAsync(TokenScope scope, CancellationToken cancellationToken);
    }
}
