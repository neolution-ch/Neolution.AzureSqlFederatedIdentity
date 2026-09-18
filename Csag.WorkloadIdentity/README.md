# Csag.WorkloadIdentity

[![NuGet](https://img.shields.io/nuget/v/Csag.WorkloadIdentity.svg)](https://www.nuget.org/packages/Csag.WorkloadIdentity)
[![License: MIT](https://img.shields.io/badge/License-MIT-lightgray.svg)](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/blob/main/LICENSE)

Passwordless access to Azure SQL for .NET applications that run on Google Cloud. The library turns the application's Google identity into a Microsoft Entra ID access token through workload identity federation; you attach that token to your `SqlConnection`. No database password, client secret or service account key is stored anywhere.

How it works:

1. Using Application Default Credentials, the library asks the IAM Service Account Credentials API for a Google-signed ID token for the configured service account, with the audience `api://AzureADTokenExchange`.
2. It presents that ID token to Microsoft Entra ID as a client assertion for your app registration, which trusts the service account through a federated credential, and receives an access token for Azure SQL (scope `https://database.windows.net/.default`).
3. Your code assigns the access token to `SqlConnection.AccessToken` and opens the connection.

- Repository: <https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity>
- Cloud setup guide (Google Cloud, Microsoft Entra ID, Azure SQL, Cloud Run): <https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/blob/main/docs/cloud-identity-setup.md>

## Features

- Exchanges a Google-signed ID token for a Microsoft Entra ID access token for Azure SQL; there are no secrets to store or rotate.
- Holds the current access token in memory and hands it out until it enters the configured refresh-ahead window. Callers that find no usable token share a single exchange instead of each running their own.
- Background refresh (on by default): a hosted service exchanges a fresh token whenever the held one enters the refresh-ahead window, so requests are served from a valid token without waiting for an exchange. Failed exchanges are retried with exponential backoff.
- Options are validated when the host starts, so a missing or invalid value fails fast with a message that names it.
- The public services, `IAzureSqlTokenProvider`, `IAzureSqlTokenExchanger` and `IGoogleIdTokenProvider`, are registered with `TryAdd`, so you can replace any of them by registering your own implementation first. The background refresh hosted service is always added; turn it off with `EnableBackgroundRefresh` instead.
- Targets `net8.0` and `net10.0`.

## Prerequisites

- An application on .NET 8 or .NET 10 that uses `Microsoft.Extensions.DependencyInjection` (ASP.NET Core, a worker service or the generic host). Microsoft's support for .NET 8 ends on 10 November 2026.
- **Google Cloud:** a service account; the IAM Service Account Credentials API enabled in its project (`gcloud services enable iamcredentials.googleapis.com`); and the identity the application runs as granted **Service Account OpenID Connect Identity Token Creator** (`roles/iam.serviceAccountOpenIdTokenCreator`) on that service account.
- **Microsoft Entra ID:** an app registration with a federated credential that trusts the service account (issuer `https://accounts.google.com`, subject = the service account's unique ID, audience `api://AzureADTokenExchange`).
- **Azure SQL:** a contained database user for the app registration (`CREATE USER [...] FROM EXTERNAL PROVIDER`) with the permissions your application needs.
- **Application Default Credentials at runtime:** on Cloud Run, GKE or Compute Engine the attached service account; on a workstation `gcloud auth application-default login`.

The setup guide linked above walks through each of these step by step.

## Quickstart

### 1. Install

```shell
dotnet add package Csag.WorkloadIdentity
```

The library returns a token string and does not depend on `Microsoft.Data.SqlClient`. Add that package, or an EF Core provider such as `Microsoft.EntityFrameworkCore.SqlServer`, to the project that opens the connections.

### 2. Configure

The options bind from the `Csag.WorkloadIdentity` section of the configuration:

```json
{
  "Csag.WorkloadIdentity": {
    "TenantId": "<entra-tenant-id>",
    "ClientId": "<app-registration-client-id>",
    "Google": {
      "ServiceAccountEmail": "<name>@<project-id>.iam.gserviceaccount.com"
    },
    "RefreshAheadWindow": "00:05:00",
    "EnableBackgroundRefresh": true
  },
  "ConnectionStrings": {
    "AzureSql": "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True"
  }
}
```

| Key | Required | Default | Description |
|---|---|---|---|
| `TenantId` | yes | – | Directory (tenant) ID of the Microsoft Entra tenant. |
| `ClientId` | yes | – | Application (client) ID of the app registration that holds the federated credential. |
| `Google:ServiceAccountEmail` | yes | – | The Google service account the ID token is minted for. The federated credential names this account's unique ID as its subject. |
| `RefreshAheadWindow` | no | `00:05:00` | How long before the access token expires it is treated as due for refresh. Must be positive. |
| `EnableBackgroundRefresh` | no | `true` | Whether a hosted service keeps the token refreshed ahead of its expiry. |

Environment variables follow the usual .NET mapping, with `__` as the section separator: `Csag.WorkloadIdentity__TenantId`, `Csag.WorkloadIdentity__ClientId`, `Csag.WorkloadIdentity__Google__ServiceAccountEmail`, and so on.

The connection string deliberately carries no credentials; see the notes below.

### 3. Register

```csharp
using Csag.WorkloadIdentity;

var builder = WebApplication.CreateBuilder(args);

// Binds the "Csag.WorkloadIdentity" section of the host configuration.
builder.Services.AddAzureSqlFederatedIdentity();

var app = builder.Build();
app.Run();
```

Two further overloads exist, for a host whose configuration is not registered as `IConfiguration` in the container and for configuring in code. Given an `IServiceCollection services` and an `IConfiguration configuration`:

```csharp
using Csag.WorkloadIdentity;
using Csag.WorkloadIdentity.Options;

// Binds the "Csag.WorkloadIdentity" section of the given configuration.
services.AddAzureSqlFederatedIdentity(configuration);

// Sets the options in code; the section name is available as AzureSqlFederatedIdentityOptions.ConfigurationSectionName.
services.AddAzureSqlFederatedIdentity(options =>
{
    options.TenantId = "<entra-tenant-id>";
    options.ClientId = "<app-registration-client-id>";
    options.Google = new GoogleOptions { ServiceAccountEmail = "<name>@<project-id>.iam.gserviceaccount.com" };
    options.RefreshAheadWindow = TimeSpan.FromMinutes(10);
    options.EnableBackgroundRefresh = true;
});
```

`GoogleOptions` and `AzureSqlFederatedIdentityOptions` live in `Csag.WorkloadIdentity.Options`. The options are validated when the host starts: a missing `TenantId`, `ClientId` or `Google:ServiceAccountEmail`, or a non-positive `RefreshAheadWindow`, throws an `OptionsValidationException` that names the offending value. Calling `AddAzureSqlFederatedIdentity` more than once is harmless.

### 4. Use the token

Resolve `IAzureSqlTokenProvider` (namespace `Csag.WorkloadIdentity.Abstractions`), call `GetAzureSqlAccessTokenAsync` and assign the result to `SqlConnection.AccessToken` before opening the connection. Do this for every new connection: until the held token enters the refresh-ahead window the call returns it without any network round trip; inside the window (reachable only when background refresh is off or has been failing) the first caller exchanges a new token and concurrent callers wait for that one exchange.

```csharp
using Csag.WorkloadIdentity.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public sealed class OrderRepository(IConfiguration configuration, IAzureSqlTokenProvider tokenProvider)
{
    private readonly string connectionString = configuration.GetConnectionString("AzureSql")
        ?? throw new InvalidOperationException("Connection string 'AzureSql' is not configured.");

    public async Task<int> CountOrdersAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(this.connectionString);
        connection.AccessToken = await tokenProvider.GetAzureSqlAccessTokenAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Orders", connection);
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }
}
```

Register the class as usual, for example `builder.Services.AddScoped<OrderRepository>();`.

#### With Entity Framework Core

EF Core has no option for the access token, so set it on the underlying `SqlConnection` when you create the context. This factory follows the pattern the repository's Demo project uses (`AppDbContext` is your `DbContext`):

```csharp
using Csag.WorkloadIdentity.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public sealed class AppDbContextFactory(IConfiguration configuration, IAzureSqlTokenProvider tokenProvider)
{
    private readonly DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(configuration.GetConnectionString("AzureSql")
            ?? throw new InvalidOperationException("Connection string 'AzureSql' is not configured."))
        .Options;

    public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        // The connection string carries no credentials; the federated access token authenticates the connection.
        var accessToken = await tokenProvider.GetAzureSqlAccessTokenAsync(cancellationToken);

        var context = new AppDbContext(this.options);
        if (context.Database.GetDbConnection() is SqlConnection sqlConnection)
        {
            sqlConnection.AccessToken = accessToken;
        }

        return context;
    }
}
```

Register the factory (`builder.Services.AddScoped<AppDbContextFactory>();`) and call `CreateDbContextAsync` wherever you need a context. If you prefer `AddDbContext`, make the same assignment from a `DbConnectionInterceptor` that overrides `ConnectionOpeningAsync`.

## Notes

- **Connection string.** The access token is the credential, so the connection string must not contain `User ID`/`Password`, `Integrated Security` or an `Authentication` keyword: `SqlClient` throws an `InvalidOperationException` when `AccessToken` is combined with conflicting authentication settings. Keep `Encrypt=True` so the token and your data travel over TLS.
- **Runtime identity.** The Google ID token is requested through Application Default Credentials (ADC). Whatever identity ADC resolves to (the service account attached to the Cloud Run, GKE or Compute Engine resource, or your own account after `gcloud auth application-default login`) must hold `roles/iam.serviceAccountOpenIdTokenCreator` on the configured `ServiceAccountEmail`. The intended deployment is the simplest one: the application runs *as* that service account, with the role granted to the account on itself.
- **Token handling.** The provider holds the token; do not cache or persist it yourself. `AccessToken` is part of the `SqlClient` connection pool key, so a refreshed token starts a new pool, which is expected. If your connection string sets `Min Pool Size` above zero, call `SqlConnection.ClearPool` with a connection that carries the old token once it has expired; otherwise the pool keeps that token's physical connections open indefinitely (see the [`AccessToken` remarks](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.accesstoken)).
- **Background refresh.** When enabled, the hosted service exchanges a token as soon as the host starts and again when the held token enters the refresh-ahead window, or at half the token's remaining lifetime when that is shorter than the window (the wait is kept between 10 seconds and 1 day). A failed exchange is logged at `Error` and retried, waiting 5 seconds and doubling up to 5 minutes between attempts. When disabled, the first caller to find the token due for refresh performs the exchange while concurrent callers wait for its result. If you register your own `IAzureSqlTokenProvider`, the hosted service leaves it alone.
- **Logging.** All categories start with `Csag.WorkloadIdentity`. Exchanges and refreshes log at `Debug`; per-call reuse of the held token logs at `Trace`.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| The host fails to start with `OptionsValidationException` | A required key is missing or `RefreshAheadWindow` is not positive; the message names the value. |
| `RpcException` with status `PermissionDenied` in the log; the caller receives it as the inner exception of an `AuthenticationFailedException` | The runtime identity lacks `roles/iam.serviceAccountOpenIdTokenCreator` on the service account, or the IAM Service Account Credentials API is not enabled in the project. |
| "Failed to create IAMCredentialsClient" in the log | No Application Default Credentials were found; set the runtime service account, or run `gcloud auth application-default login` on a workstation. |
| Microsoft Entra ID rejects the assertion because no matching federated identity credential was found | The federated credential's issuer, subject (the service account's unique ID) or audience does not match the ID token. |
| Azure SQL reports "Login failed for user" | No database user exists for the app registration in the target database, or the server's network rules block the connection. |

## License

MIT. See <https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/blob/main/LICENSE>.

## Contributing and security

Issues and pull requests are welcome at <https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity>; the repository's `CONTRIBUTING.md` explains the build, the tests and the release process. Please report vulnerabilities privately through GitHub's private vulnerability reporting: <https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/security/advisories/new>.
