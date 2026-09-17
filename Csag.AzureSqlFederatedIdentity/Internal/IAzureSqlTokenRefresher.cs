namespace Csag.AzureSqlFederatedIdentity.Internal
{
    /// <summary>
    /// Replaces the currently held Azure SQL access token with a freshly exchanged one, regardless of how much
    /// lifetime the current token has left. Implemented by the default token provider so that the background
    /// refresh service can renew the token ahead of its expiry.
    /// </summary>
    internal interface IAzureSqlTokenRefresher
    {
        /// <summary>
        /// Exchanges a fresh Azure SQL access token and makes it the current token.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The instant at which the token now held expires.</returns>
        Task<DateTimeOffset> RefreshAsync(CancellationToken cancellationToken);
    }
}
