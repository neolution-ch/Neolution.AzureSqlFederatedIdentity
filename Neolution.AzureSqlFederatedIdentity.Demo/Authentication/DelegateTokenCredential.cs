namespace Neolution.AzureSqlFederatedIdentity.Demo.Authentication
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;

    /// <summary>
    /// A delegate-based TokenCredential that wraps a Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>>.
    /// </summary>
    public class DelegateTokenCredential : TokenCredential
    {
        private readonly Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>> getToken;

        public DelegateTokenCredential(Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>> getToken)
            => this.getToken = getToken ?? throw new ArgumentNullException(nameof(getToken));

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => this.getToken(requestContext, cancellationToken).GetAwaiter().GetResult();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext,
            CancellationToken cancellationToken)
            => this.getToken(requestContext, cancellationToken);
    }
}
