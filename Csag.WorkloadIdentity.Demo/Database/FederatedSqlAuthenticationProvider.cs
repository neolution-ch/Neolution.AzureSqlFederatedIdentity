namespace Csag.WorkloadIdentity.Demo.Database
{
    using System;
    using Csag.WorkloadIdentity.Abstractions;
    using Microsoft.Data.SqlClient;

    // SqlClient asks this provider for the access token of every connection whose connection string says
    // "Authentication=Active Directory Default", including connections EF Core does not open itself.
    internal sealed class FederatedSqlAuthenticationProvider(IAzureSqlTokenProvider tokenProvider) : SqlAuthenticationProvider
    {
        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
            => authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDefault;

        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            // ConnectionTimeout bounds the token request; 0 means no limit.
            using var timeout = new CancellationTokenSource();
            if (parameters.ConnectionTimeout > 0)
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(parameters.ConnectionTimeout));
            }

            var accessToken = await tokenProvider.GetAccessTokenAsync(timeout.Token);
            return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
        }
    }
}
