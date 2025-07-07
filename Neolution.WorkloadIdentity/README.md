# Neolution.WorkloadIdentity

[![NuGet](https://img.shields.io/nuget/v/Neolution.WorkloadIdentity.svg)](https://www.nuget.org/packages/Neolution.WorkloadIdentity)
[![License: MIT](https://img.shields.io/badge/License-MIT-lightgray.svg)](../LICENSE)

Federated identity integration for Azure SQL using Google Cloud IAM Credentials and Azure AD.

## Features

- Obtain Azure SQL access tokens (Azure Managed Identity or Google federated identity)
- Obtain Azure Blob Storage tokens (Azure Managed Identity or Google federated identity)
- Automatic token refresh and in-memory caching
- Easy integration with ASP.NET Core and .NET worker services

## Getting Started

1. Install the NuGet package:

   ```shell
   dotnet add package Neolution.WorkloadIdentity
   ```

2. Configure your `appsettings.json` (using system-assigned Managed Identity):

   ```json
   {
     "Neolution.WorkloadIdentity": {
       "AzureSql": {
         "Provider": "ManagedIdentity",
         "ManagedIdentity": {
           "UseSystemAssignedIdentity": true
         }
       }
     }
   }
   ```

3. Register the services in your `Program.cs`:

   ```csharp
   builder.Services.AddWorkloadIdentity(builder.Configuration);
   ```

## Configuration

This library adopts a **resource-first** model: each built-in token provider (e.g. Azure SQL, Azure Blob Storage) gets its own section under `Neolution.WorkloadIdentity`.  You choose a `Provider` and supply only the settings that apply to that identity flow.

Key design points:

1. **Separation of concerns**: resource sections (`AzureSql`, `BlobStorage`) map directly to the corresponding token provider classes.
2. **Provider switch**: the `Provider` field determines whether we use:
   - `ManagedIdentity` (Azure AD Managed Identity via `Azure.Identity`)
   - `Google` (Google Workload Identity Federation to mint Azure AD tokens)
3. **Strongly typed options**: each provider block has its own options class (`ManagedIdentityOptions`, `GoogleOptions`) and built-in validators to prevent misconfiguration.

### Example scenarios

1. Azure App Service with user-assigned Managed Identity for both Azure SQL and Blob Storage:

   ```json
   {
     "Neolution.WorkloadIdentity": {
       "AzureSql": {
         "Provider": "ManagedIdentity",
         "ManagedIdentity": {
           "UseSystemAssignedIdentity": false,
           "ClientId": "00000000-0000-0000-0000-000000000000"
         }
       },
       "BlobStorage": {
         "Provider": "ManagedIdentity",
         "ManagedIdentity": {
           "UseSystemAssignedIdentity": false,
           "ClientId": "00000000-0000-0000-0000-000000000000"
         }
       }
     }
   }
   ```

2. Azure App Service using Azure SQL via system-assigned Managed Identity and Blob Storage via user-assigned Managed Identity:

   ```json
   {
     "Neolution.WorkloadIdentity": {
       "AzureSql": {
         "Provider": "ManagedIdentity",
         "ManagedIdentity": { "UseSystemAssignedIdentity": true }
       },
       "BlobStorage": {
         "Provider": "ManagedIdentity",
         "ManagedIdentity": {
           "UseSystemAssignedIdentity": false,
           "ClientId": "00000000-0000-0000-0000-000000000000"
         }
       }
     }
   }
   ```

3. Google Cloud Run with Google federated identity for both Azure SQL and Azure Blob Storage:

   ```json
   {
     "Neolution.WorkloadIdentity": {
       "AzureSql": {
         "Provider": "Google",
         "Google": {
           "TenantId": "<your-azure-ad-tenant-id>",
           "ClientId": "<your-uami-client-id>",
           "ServiceAccountEmail": "runner@project.iam.gserviceaccount.com"
         }
       },
       "BlobStorage": {
         "Provider": "Google",
         "Google": {
           "TenantId": "<your-azure-ad-tenant-id>",
           "ClientId": "<your-uami-client-id>",
           "ServiceAccountEmail": "runner@project.iam.gserviceaccount.com"
         }
       }
     }
   }
   ```

   > Note: `ClientId` and `TenantId` refer to your Azure AD user-assigned managed identity (UAMI) client ID and tenant ID. A user-assigned managed identity is a managed identity resource you create in Azure AD, which can be assigned to one or more Azure resources. You can find the client ID and tenant ID in the Azure Portal under the managed identity's properties. `ServiceAccountEmail` is your Google service account email.

## Usage

### IAzureSqlTokenProvider

When opening a connection, inject `IAzureSqlTokenProvider`:

   ```csharp
   var tokenProvider = sp.GetRequiredService<IAzureSqlTokenProvider>();
   var token = await tokenProvider.GetAzureSqlAccessTokenAsync(cancellationToken);
   var connection = new SqlConnection(connStr) { AccessToken = token };
   ```

#### Using with `DbContext` factory

   ```csharp
   public class AppDbContextFactory : IAppDbContextFactory
   {
       private readonly DbContextOptions<AppDbContext> options;
       private readonly IAzureSqlTokenProvider tokenProvider;

       public AppDbContextFactory(IConfiguration config, IAzureSqlTokenProvider tokenProvider)
       {
           var connStr = config.GetConnectionString("DefaultConnection");
           options = new DbContextOptionsBuilder<AppDbContext>()
               .UseSqlServer(connStr)
               .Options;
           this.tokenProvider = tokenProvider;
       }

       public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
       {
           var context = new AppDbContext(options);
           if (context.Database.GetDbConnection() is SqlConnection sqlConn)
           {
               sqlConn.AccessToken = await tokenProvider.GetAzureSqlAccessTokenAsync(cancellationToken);
           }
           return context;
       }
   }
   ```

#### Using with `DbContext` directly

   ```csharp
   // Program.cs
   builder.Services.AddDbContext<AppDbContext>(async (sp, options) =>
   {
       var tokenProvider = sp.GetRequiredService<IAzureSqlTokenProvider>();
       var token = await tokenProvider.GetAzureSqlAccessTokenAsync();
       var connection = new SqlConnection("<your-connection-string>") { AccessToken = token };
       options.UseSqlServer(connection);
   });
   ```

Now all connections to Azure SQL will use the managed token instead of a password.

### IBlobStorageTokenProvider

Inject `IBlobStorageTokenProvider` to fetch a token:

   ```csharp
   var blobProvider = sp.GetRequiredService<IBlobStorageTokenProvider>();
   var token = await blobProvider.GetBlobStorageAccessTokenAsync(cancellationToken);
   ```

#### Using a `BlobServiceClient` with a delegate credential

   ```csharp
   var credential = new DelegateTokenCredential(async (ctx, ct) => token);
   var client = new BlobServiceClient(
     new Uri($"https://{accountName}.blob.core.windows.net"),
     credential
   );
   ```

## Roadmap & Vision

- **Multi-cloud direction**: Enable connections not only from Azure or Google to Azure, but also from Azure to Google (e.g., Azure to Google Cloud SQL, Google Cloud Storage, etc.).
- **More resource types**: Add support for additional resource types (e.g. Service Bus, Pub/Sub etc.).
- **Other cloud providers**: Extend support to other cloud and token providers.

## License

MIT

## Contributing

Contributions are welcome! Please open issues or pull requests.
