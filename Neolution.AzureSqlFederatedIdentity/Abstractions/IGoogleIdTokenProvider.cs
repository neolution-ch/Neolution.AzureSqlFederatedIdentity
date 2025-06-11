namespace Neolution.AzureSqlFederatedIdentity.Abstractions
{
    /// <summary>
    /// Provides a method to obtain a Google-signed ID token for the target service account.
    /// </summary>
    public interface IGoogleIdTokenProvider
    {
        /// <summary>
        /// Returns an ID token for the target service account, with audience = Azure AD client ID.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The Google-signed ID token as a string.</returns>
        Task<string> GetIdTokenAsync(CancellationToken cancellationToken);
    }
}
