# Csag.WorkloadIdentity

[![NuGet](https://img.shields.io/nuget/v/Csag.WorkloadIdentity.svg)](https://www.nuget.org/packages/Csag.WorkloadIdentity)
[![License: MIT](https://img.shields.io/badge/License-MIT-lightgray.svg)](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/blob/main/LICENSE)

Passwordless access tokens for Azure SQL and Azure Blob Storage, obtained from the identity a .NET application already runs as. The model is resource-first: each Azure resource the application uses has its own configuration section that selects the identity provider the token comes from, either the **managed identity** of the Azure resource the application runs on (`ManagedIdentity`) or the application's **Google identity**, exchanged for a Microsoft Entra ID access token through workload identity federation (`Google`). The library holds one access token per resource, refreshes it ahead of expiry, and hands it to your code as a string for `SqlConnection.AccessToken` or as a `TokenCredential` for Azure SDK clients such as `BlobServiceClient`. No database password, storage key, client secret or service account key is stored anywhere.

How a token is obtained, per provider:

- **`ManagedIdentity`.** The library requests the token from the managed identity endpoint of the Azure resource the application runs on (App Service, Container Apps, Functions, a virtual machine, AKS and so on), as the system-assigned identity of that resource or as a user-assigned identity selected by its client ID.
- **`Google`.** Using Application Default Credentials, the library asks the IAM Service Account Credentials API for a Google-signed ID token for the configured service account with the audience `api://AzureADTokenExchange`, and presents that ID token to Microsoft Entra ID as the client assertion of the identity that holds a federated credential trusting the service account: an app registration or a user-assigned managed identity.

Either way the result is a Microsoft Entra ID access token for the resource's scope, `https://database.windows.net/.default` for Azure SQL and `https://storage.azure.com/.default` for Blob Storage.

- Repository: <https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity>
- Cloud and identity setup guide (managed identities, Google Cloud to Microsoft Entra ID federation, Azure SQL users, Blob Storage roles, deployment): <https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/blob/main/docs/cloud-identity-setup.md>

## Features

- Access tokens for **Azure SQL** (`IAzureSqlTokenProvider`) and **Azure Blob Storage** (`IBlobStorageTokenProvider`). Each resource is configured on its own, so the two can use different identities or different providers.
- Two identity providers with no secrets to store or rotate: the Azure managed identity the application runs as, or its Google identity through workload identity federation.
- `WorkloadIdentityTokenCredential` presents any of the providers to Azure SDK clients as a `TokenCredential`, for example to a `BlobServiceClient`.
- Holds the current access token of each resource in memory and hands it out until it enters the configured refresh-ahead window. Callers that find no usable token share a single token request instead of each running their own.
- Background refresh (on by default): a hosted service refreshes each resource's token ahead of its expiry, so requests are served from a valid token without waiting for a token request. Failed requests are retried with exponential backoff.
- Options are validated when the host starts, so a missing or invalid value fails fast with a message that names it.
- Every service is registered with `TryAdd`, so you can replace any part of the pipeline, including a token provider, by registering your own implementation first.
- Targets `net8.0` and `net10.0`.

## Prerequisites

- An application on .NET 8 or .NET 10 that uses `Microsoft.Extensions.DependencyInjection` (ASP.NET Core, a worker service or the generic host). Microsoft's support for .NET 8 ends on 10 November 2026.
- **With `ManagedIdentity`:** the application runs on an Azure resource that has a system-assigned managed identity enabled or a user-assigned managed identity attached. That identity has a database user in Azure SQL (`CREATE USER [...] FROM EXTERNAL PROVIDER`) and/or a Blob data role (`Storage Blob Data Reader` or `Storage Blob Data Contributor`) on the storage account.
- **With `Google`:** a Google service account, with the IAM Service Account Credentials API enabled in its project (`gcloud services enable iamcredentials.googleapis.com`) and the identity the application runs as granted **Service Account OpenID Connect Identity Token Creator** (`roles/iam.serviceAccountOpenIdTokenCreator`) on it; a Microsoft Entra ID app registration or user-assigned managed identity with a federated credential that trusts the service account (issuer `https://accounts.google.com`, subject = the service account's unique ID, audience `api://AzureADTokenExchange`), holding the same database user and/or Blob role; and Application Default Credentials at runtime (on Cloud Run, GKE or Compute Engine the attached service account; on a workstation `gcloud auth application-default login`).

The setup guide linked above walks through each of these step by step.

## Quickstart A: Azure SQL from Google Cloud Run

An application on Cloud Run runs as a Google service account, so its Azure SQL token comes from the `Google` provider.

### 1. Install

```shell
dotnet add package Csag.WorkloadIdentity
```

The library returns a token string and does not depend on `Microsoft.Data.SqlClient`. Add that package, or an EF Core provider such as `Microsoft.EntityFrameworkCore.SqlServer`, to the project that opens the connections.

### 2. Configure

The options bind from the `Csag.WorkloadIdentity` section of the configuration. The `AzureSql` section selects the provider and carries that provider's settings:

```json
{
  "Csag.WorkloadIdentity": {
    "AzureSql": {
      "Provider": "Google",
      "Google": {
        "TenantId": "<entra-tenant-id>",
        "ClientId": "<client-id-of-the-federated-credential-holder>",
        "ServiceAccountEmail": "<name>@<project-id>.iam.gserviceaccount.com"
      }
    }
  },
  "ConnectionStrings": {
    "AzureSql": "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True"
  }
}
```

`ClientId` is the application (client) ID of the app registration, or the client ID of the user-assigned managed identity, that holds the federated credential. On Cloud Run supply the same keys as environment variables, with `__` as the section separator: `Csag.WorkloadIdentity__AzureSql__Provider`, `Csag.WorkloadIdentity__AzureSql__Google__TenantId`, `Csag.WorkloadIdentity__AzureSql__Google__ClientId`, `Csag.WorkloadIdentity__AzureSql__Google__ServiceAccountEmail` and `ConnectionStrings__AzureSql`. The connection string deliberately carries no credentials; see the notes below.

### 3. Register

```csharp
using Csag.WorkloadIdentity;

var builder = WebApplication.CreateBuilder(args);

// Binds the "Csag.WorkloadIdentity" section of the host configuration.
builder.Services.AddWorkloadIdentity();

var app = builder.Build();
app.Run();
```

The options reference below shows the other two overloads: binding a given `IConfiguration`, and configuring the options in code.

### 4. Use the token

Resolve `IAzureSqlTokenProvider` (namespace `Csag.WorkloadIdentity.Abstractions`), call `GetAzureSqlAccessTokenAsync` and assign the result to `SqlConnection.AccessToken` before opening the connection. Do this for every new connection: while the held token is valid the call returns it without any network round trip.

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
        // The connection string carries no credentials; the access token authenticates the connection.
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

## Quickstart B: Azure SQL and Blob Storage from Azure App Service

An application on App Service (or Container Apps, Functions, a virtual machine, AKS) has a managed identity, so both tokens come from the `ManagedIdentity` provider. Blob Storage is reached through the Azure SDK, with `WorkloadIdentityTokenCredential` adapting the library's provider to the `TokenCredential` the SDK expects.

### 1. Install

```shell
dotnet add package Csag.WorkloadIdentity
dotnet add package Azure.Storage.Blobs
```

Add `Microsoft.Data.SqlClient` or an EF Core provider for Azure SQL as in quickstart A.

### 2. Configure

With the App Service's system-assigned identity for both resources:

```json
{
  "Csag.WorkloadIdentity": {
    "AzureSql": {
      "Provider": "ManagedIdentity",
      "ManagedIdentity": { "UseSystemAssignedIdentity": true }
    },
    "BlobStorage": {
      "Provider": "ManagedIdentity",
      "ManagedIdentity": { "UseSystemAssignedIdentity": true }
    }
  },
  "ConnectionStrings": {
    "AzureSql": "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True"
  },
  "BlobStorage": {
    "ServiceUri": "https://<storage-account>.blob.core.windows.net"
  }
}
```

For a user-assigned managed identity, name it by its client ID instead:

```json
"ManagedIdentity": { "ClientId": "<user-assigned-managed-identity-client-id>" }
```

`Provider` defaults to `ManagedIdentity`, so it could be omitted here; it is written out for clarity. The two resources are independent: one can use the system-assigned identity and the other a user-assigned one, or the `Google` provider. In App Service, supply the values as application settings with the same `__` separator, for example `Csag.WorkloadIdentity__AzureSql__ManagedIdentity__UseSystemAssignedIdentity` = `true`. `BlobStorage:ServiceUri` is the application's own setting, read below.

### 3. Register

```csharp
using Azure.Storage.Blobs;
using Csag.WorkloadIdentity;
using Csag.WorkloadIdentity.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWorkloadIdentity();

// One BlobServiceClient for the application. The adapter serves every token request of the client from the
// library's held Blob Storage token, so the client needs neither a key nor a connection string.
builder.Services.AddSingleton(serviceProvider => new BlobServiceClient(
    new Uri(builder.Configuration["BlobStorage:ServiceUri"]
        ?? throw new InvalidOperationException("Setting 'BlobStorage:ServiceUri' is not configured.")),
    new WorkloadIdentityTokenCredential(serviceProvider.GetRequiredService<IBlobStorageTokenProvider>())));

var app = builder.Build();
app.Run();
```

Azure SDK clients are thread-safe and meant to be shared, so a singleton is the right lifetime for the `BlobServiceClient`.

### 4. Use the tokens

Azure SQL works exactly as in quickstart A: `IAzureSqlTokenProvider` and `SqlConnection.AccessToken`, with plain ADO.NET or the EF Core factory. For Blob Storage inject the `BlobServiceClient`; the SDK asks the credential for a token on every request and the adapter answers from the held token:

```csharp
using Azure.Storage.Blobs;

public sealed class DocumentStore(BlobServiceClient blobServiceClient)
{
    public async Task<string> ReadTextAsync(string containerName, string blobName, CancellationToken cancellationToken)
    {
        var blobClient = blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        var download = await blobClient.DownloadContentAsync(cancellationToken);
        return download.Value.Content.ToString();
    }
}
```

`IBlobStorageTokenProvider.GetBlobStorageAccessTokenAsync` returns the raw token string for code that calls the Blob REST API directly and sets the `Authorization: Bearer` header itself. `WorkloadIdentityTokenCredential` accepts any of the library's providers (`IAccessTokenProvider`), so the same adapter can present the Azure SQL provider to an SDK client that authenticates with a `TokenCredential`.

## Options reference

All keys live under the `Csag.WorkloadIdentity` section. `<resource>` stands for `AzureSql` or `BlobStorage`; the two sections have the same shape and are independent of each other.

| Key | Required | Default | Description |
|---|---|---|---|
| `AzureSql` | at least one resource section | – | Configures how Azure SQL tokens are obtained. Without it, resolving `IAzureSqlTokenProvider` throws. |
| `BlobStorage` | at least one resource section | – | Configures how Blob Storage tokens are obtained. Without it, resolving `IBlobStorageTokenProvider` throws. |
| `<resource>:Provider` | no | `ManagedIdentity` | `ManagedIdentity` or `Google`. Selects which of the two provider sections is read; the other is ignored. |
| `<resource>:ManagedIdentity:UseSystemAssignedIdentity` | no | `false` | `true` uses the system-assigned identity of the hosting resource; `ClientId` is then ignored. |
| `<resource>:ManagedIdentity:ClientId` | with `ManagedIdentity`, unless `UseSystemAssignedIdentity` is `true` | – | Client ID of the user-assigned managed identity. |
| `<resource>:Google:TenantId` | with `Google` | – | Directory (tenant) ID of the Microsoft Entra tenant that issues the access token. |
| `<resource>:Google:ClientId` | with `Google` | – | Application (client) ID of the app registration, or client ID of the user-assigned managed identity, that holds the federated credential. |
| `<resource>:Google:ServiceAccountEmail` | with `Google` | – | The Google service account the ID token is minted for. The federated credential names this account's unique ID as its subject. |
| `RefreshAheadWindow` | no | `00:05:00` | How long before an access token expires it is treated as due for refresh. Applies to every resource. Must be positive. |
| `EnableBackgroundRefresh` | no | `true` | Whether a hosted service keeps the token of every configured resource refreshed ahead of its expiry. |

Environment variables follow the usual .NET mapping, with `__` as the section separator and the `.` of the section name kept as is: `Csag.WorkloadIdentity__AzureSql__Provider`, `Csag.WorkloadIdentity__BlobStorage__ManagedIdentity__ClientId`, `Csag.WorkloadIdentity__RefreshAheadWindow`, and so on.

### Registering with a given configuration or in code

Besides `AddWorkloadIdentity()`, which binds the section from the `IConfiguration` registered in the container, two overloads exist. Given an `IServiceCollection services` and an `IConfiguration configuration`:

```csharp
using Csag.WorkloadIdentity;
using Csag.WorkloadIdentity.Options;

// Binds the "Csag.WorkloadIdentity" section of the given configuration.
services.AddWorkloadIdentity(configuration);

// Sets the options in code; the section name is available as WorkloadIdentityOptions.ConfigurationSectionName.
services.AddWorkloadIdentity(options =>
{
    options.AzureSql = new WorkloadIdentityResourceOptions
    {
        Provider = WorkloadIdentityProvider.Google,
        Google = new GoogleOptions
        {
            TenantId = "<entra-tenant-id>",
            ClientId = "<client-id-of-the-federated-credential-holder>",
            ServiceAccountEmail = "<name>@<project-id>.iam.gserviceaccount.com",
        },
    };
    options.BlobStorage = new WorkloadIdentityResourceOptions
    {
        Provider = WorkloadIdentityProvider.ManagedIdentity,
        ManagedIdentity = new ManagedIdentityOptions { UseSystemAssignedIdentity = true },
    };
    options.RefreshAheadWindow = TimeSpan.FromMinutes(10);
    options.EnableBackgroundRefresh = true;
});
```

`WorkloadIdentityOptions`, `WorkloadIdentityResourceOptions`, `WorkloadIdentityProvider`, `GoogleOptions` and `ManagedIdentityOptions` live in `Csag.WorkloadIdentity.Options`; the provider interfaces and `IAccessTokenProvider` in `Csag.WorkloadIdentity.Abstractions`; `AddWorkloadIdentity`, `WorkloadIdentityTokenCredential` and `TokenScope` in `Csag.WorkloadIdentity`.

The options are validated when the host starts: no resource section at all, a missing value in a configured resource's provider section, or a non-positive `RefreshAheadWindow` throws an `OptionsValidationException` that names every offending value by its path, for example `AzureSql:Google:ServiceAccountEmail must be provided.` A `Provider` value other than `ManagedIdentity` or `Google` fails earlier, when the configuration is bound. Calling `AddWorkloadIdentity` more than once is harmless. Both token providers are always registered; resolving the provider of a resource whose section is absent throws an `InvalidOperationException` that names the missing section.

## Migrating from Csag.AzureSqlFederatedIdentity / Neolution.AzureSqlFederatedIdentity

`Csag.WorkloadIdentity` continues the `Neolution.AzureSqlFederatedIdentity` package (briefly renamed `Csag.AzureSqlFederatedIdentity`, without a release under that name). Azure SQL over Google federation works as before and needs no change on the Google or Microsoft side; what changes is the naming and the shape of the configuration.

1. **Package and namespaces.** Replace the package reference with `Csag.WorkloadIdentity`, and the `Neolution.AzureSqlFederatedIdentity` (or `Csag.AzureSqlFederatedIdentity`) namespace prefix with `Csag.WorkloadIdentity` in `using` directives: `Csag.WorkloadIdentity.Abstractions` for the provider interfaces, `Csag.WorkloadIdentity.Options` for the options types.
2. **Registration.** `AddAzureSqlFederatedIdentity` becomes `AddWorkloadIdentity`, with the same three overloads (host configuration, `IConfiguration`, configure in code). `AzureSqlFederatedIdentityOptions` becomes `WorkloadIdentityOptions`, whose `ConfigurationSectionName` is `Csag.WorkloadIdentity`.
3. **Configuration.** The section is renamed and becomes resource-first: `TenantId` and `ClientId` move into the `Google` section, and that section moves under the resource it serves, `AzureSql`, next to `"Provider": "Google"`.

   Before:

   ```json
   {
     "Neolution.AzureSqlFederatedIdentity": {
       "TenantId": "<entra-tenant-id>",
       "ClientId": "<app-registration-client-id>",
       "Google": {
         "ServiceAccountEmail": "<name>@<project-id>.iam.gserviceaccount.com"
       }
     }
   }
   ```

   After:

   ```json
   {
     "Csag.WorkloadIdentity": {
       "AzureSql": {
         "Provider": "Google",
         "Google": {
           "TenantId": "<entra-tenant-id>",
           "ClientId": "<app-registration-client-id>",
           "ServiceAccountEmail": "<name>@<project-id>.iam.gserviceaccount.com"
         }
       }
     }
   }
   ```

   Environment variables move the same way: `Neolution.AzureSqlFederatedIdentity__TenantId` becomes `Csag.WorkloadIdentity__AzureSql__Google__TenantId`, and so on.
4. **Unchanged.** `IAzureSqlTokenProvider.GetAzureSqlAccessTokenAsync` and the way the token is attached to `SqlConnection.AccessToken`; the token is held in memory, and a hosted service keeps it refreshed.
5. **New or removed.** `RefreshAheadWindow` and `EnableBackgroundRefresh` are optional root keys; keep their defaults unless you have a reason not to. `IGoogleIdTokenProvider.GetIdTokenAsync` now takes the service account email, so resources can federate through different service accounts. `IAzureSqlTokenExchanger` no longer exists; the exchange is performed by internal per-provider services, and `IAzureSqlTokenProvider` remains the seam to substitute.

## Notes

- **Connection string.** The access token is the credential, so the connection string must not contain `User ID`/`Password`, `Integrated Security` or an `Authentication` keyword: `SqlClient` throws an `InvalidOperationException` when `AccessToken` is combined with conflicting authentication settings. Keep `Encrypt=True` so the token and your data travel over TLS.
- **Google runtime identity.** The Google ID token is requested through Application Default Credentials (ADC). Whatever identity ADC resolves to (the service account attached to the Cloud Run, GKE or Compute Engine resource, or your own account after `gcloud auth application-default login`) must hold `roles/iam.serviceAccountOpenIdTokenCreator` on the configured `ServiceAccountEmail`. The intended deployment is the simplest one: the application runs *as* that service account, with the role granted to the account on itself. Two resources may name different service accounts; each is exchanged through its own credential.
- **Managed identity at runtime.** The token comes from the identity endpoint of the Azure resource the application runs on, so a system-assigned identity must be enabled on that resource and a user-assigned one attached to it. A workstation has no such endpoint; `ManagedIdentity` only works on Azure. A `ClientId` next to `"UseSystemAssignedIdentity": true` is ignored.
- **Token handling.** The provider holds the token; do not cache or persist it yourself. `AccessToken` is part of the `SqlClient` connection pool key, so a refreshed token starts a new pool, which is expected. If your connection string sets `Min Pool Size` above zero, call `SqlConnection.ClearPool` with a connection that carries the old token once it has expired; otherwise the pool keeps that token's physical connections open indefinitely (see the [`AccessToken` remarks](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.accesstoken)).
- **Background refresh.** When enabled, the hosted service runs one loop per configured resource, so a slow or failing resource does not delay the other. Each loop requests a token as soon as the host starts and again when the held token enters the refresh-ahead window, or at half the token's remaining lifetime when that is shorter than the window (the wait is kept between 10 seconds and 1 day). A failed request is logged at `Error` and retried, waiting 5 seconds and doubling up to 5 minutes between attempts. When disabled, the first caller to find the token due for refresh performs the request while concurrent callers wait for its result. If you register your own `IAzureSqlTokenProvider` or `IBlobStorageTokenProvider`, the hosted service leaves it alone.
- **Logging.** All categories start with `Csag.WorkloadIdentity`. Token requests and refreshes log at `Debug`; per-call reuse of the held token logs at `Trace`.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| The host fails to start with `OptionsValidationException` | No resource section, a required key missing in a configured resource, or a non-positive `RefreshAheadWindow`; the message names the value. |
| The host fails to start with `InvalidOperationException: Failed to convert configuration value … 'Provider'` | The `Provider` value is not `ManagedIdentity` or `Google`. |
| `InvalidOperationException`: "The AzureSql resource is not configured" (or `BlobStorage`) | `IAzureSqlTokenProvider` or `IBlobStorageTokenProvider` was resolved, but the configuration has no section for that resource. |
| `CredentialUnavailableException`: no managed identity endpoint found | `ManagedIdentity` is selected but the application is not running on an Azure resource with a managed identity, for example on a workstation. |
| The managed identity endpoint reports that the identity was not found | The user-assigned identity is not attached to the hosting resource, or `ManagedIdentity:ClientId` is wrong. |
| `RpcException` with status `PermissionDenied` in the log; the caller receives it as the inner exception of an `AuthenticationFailedException` | The runtime identity lacks `roles/iam.serviceAccountOpenIdTokenCreator` on the service account, or the IAM Service Account Credentials API is not enabled in the project. |
| "Failed to create IAMCredentialsClient" in the log | No Application Default Credentials were found; set the runtime service account, or run `gcloud auth application-default login` on a workstation. |
| Microsoft Entra ID rejects the assertion because no matching federated identity credential was found | The federated credential's issuer, subject (the service account's unique ID) or audience does not match the ID token, or `Google:ClientId` names a different identity than the one holding the credential. |
| Azure SQL reports "Login failed for user" | No database user exists for the identity in the target database, or the server's network rules block the connection. |
| Blob Storage returns `403` with `AuthorizationPermissionMismatch` | The identity has no Blob data role on the storage account or container, or the role assignment has not propagated yet (it can take up to 10 minutes). |

## License

MIT. See <https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/blob/main/LICENSE>.

## Contributing and security

Issues and pull requests are welcome at <https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity>; the repository's `CONTRIBUTING.md` explains the build, the tests and the release process. Please report vulnerabilities privately through GitHub's private vulnerability reporting: <https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/security/advisories/new>.
