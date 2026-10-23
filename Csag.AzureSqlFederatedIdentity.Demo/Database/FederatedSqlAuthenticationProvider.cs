namespace Csag.AzureSqlFederatedIdentity.Demo.Database
{
    using System;
    using System.Buffers.Text;
    using System.Text.Json;
    using Csag.AzureSqlFederatedIdentity.Abstractions;
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

            // ConnectionTimeout bounds the exchange; 0 means no limit.
            using var timeout = new CancellationTokenSource();
            if (parameters.ConnectionTimeout > 0)
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(parameters.ConnectionTimeout));
            }

            var accessToken = await tokenProvider.GetAzureSqlAccessTokenAsync(timeout.Token);
            return new SqlAuthenticationToken(accessToken, ReadExpiry(accessToken));
        }

        private static DateTimeOffset ReadExpiry(string accessToken)
        {
            // A JWT is header.payload.signature, each part base64url-encoded; the payload's exp claim is in Unix seconds.
            var payload = accessToken.Split('.')[1];
            using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(payload));
            return DateTimeOffset.FromUnixTimeSeconds(claims.RootElement.GetProperty("exp").GetInt64());
        }
    }
}
