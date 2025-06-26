namespace Neolution.AzureSqlFederatedIdentity.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;

    /// <summary>
    /// Defines a contract for exchanging workload identity tokens.
    /// </summary>
    public interface IWorkloadIdentityTokenExchanger
    {
        /// <summary>
        /// Asynchronously retrieves an access token for the specified logical context.
        /// </summary>
        /// <param name="context">The logical context for which the access token is requested.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the access token.</returns>
        Task<AccessToken> GetTokenAsync(TokenContext context, CancellationToken cancellationToken);
    }
}
