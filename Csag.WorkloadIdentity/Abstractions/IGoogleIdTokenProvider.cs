namespace Csag.WorkloadIdentity.Abstractions
{
    /// <summary>
    /// Provides Google-signed ID tokens for a service account, for use as client assertions.
    /// </summary>
    public interface IGoogleIdTokenProvider
    {
        /// <summary>
        /// Returns an ID token for the service account with audience <c>api://AzureADTokenExchange</c>, the audience
        /// Microsoft Entra ID expects from a client assertion in workload identity federation.
        /// </summary>
        /// <param name="serviceAccountEmail">The email of the service account the token is minted for.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The Google-signed ID token as a string.</returns>
        Task<string> GetIdTokenAsync(string serviceAccountEmail, CancellationToken cancellationToken);
    }
}
