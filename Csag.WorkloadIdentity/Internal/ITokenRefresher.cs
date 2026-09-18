namespace Csag.WorkloadIdentity.Internal
{
    /// <summary>
    /// Replaces the currently held access token with a freshly obtained one, regardless of how much lifetime the
    /// current token has left. Implemented by the default token providers so that the background refresh service can
    /// renew their tokens ahead of expiry.
    /// </summary>
    internal interface ITokenRefresher
    {
        /// <summary>
        /// Obtains a fresh access token and makes it the current token.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The instant at which the token now held expires.</returns>
        Task<DateTimeOffset> RefreshAsync(CancellationToken cancellationToken);
    }
}
