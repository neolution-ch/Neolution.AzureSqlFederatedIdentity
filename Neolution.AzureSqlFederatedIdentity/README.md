# Neolution.AzureSqlFederatedIdentity

[![NuGet](https://img.shields.io/nuget/v/Neolution.AzureSqlFederatedIdentity.svg)](https://www.nuget.org/packages/Neolution.AzureSqlFederatedIdentity)
[![License: MIT](https://img.shields.io/badge/License-MIT-lightgray.svg)](../LICENSE)

Federated identity integration for Azure SQL using Google Cloud IAM Credentials and Azure AD.

## Features

- Obtain Azure SQL access tokens using Google service accounts
- Automatic token refresh and caching
- Easy integration with ASP.NET Core and .NET worker services

## Getting Started

1. Install the NuGet package:

   ```shell
   dotnet add package Neolution.AzureSqlFederatedIdentity
   ```

2. Configure your `appsettings.json`:   

   ```json
   {
     "Neolution.AzureSqlFederatedIdentity": {
       "TenantId": "<your-azure-ad-tenant-id>",
       "ClientId": "<your-azure-ad-client-id>",
       "Google": {
         "ServiceAccountEmail": "<your-gcp-service-account-email>"
       }
     }
   }
   ```

3. Register the services in your `Program.cs`:

   ```csharp
   builder.Services.AddAzureSqlFederatedIdentity(builder.Configuration);
   ```

## Passwordless Azure SQL Connection

You can connect to Azure SQL without passwords by leveraging the token provider from this package. The general steps are:

1. Register the services of this package in `Program.cs`:

   ```csharp
   builder.Services.AddAzureSqlFederatedIdentity(builder.Configuration);
   ```

2. Retrieve an access token when opening a SQL connection:

   ```csharp
   // Resolve the token provider
   var tokenProvider = app.Services.GetRequiredService<IAzureSqlTokenProvider>();
   var token = await tokenProvider.GetAzureSqlAccessTokenAsync();
   ```

### Example using a `DbContext` factory

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
        tokenProvider = tokenProvider;
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

### Example using `DbContext` directly

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var tokenProvider = sp.GetRequiredService<IAzureSqlTokenProvider>();
    var connection = new SqlConnection(yourConnectionString)
    {
        AccessToken = await tokenProvider.GetAzureSqlAccessTokenAsync()
    };
    options.UseSqlServer(connection);
});
```

Now all connections to Azure SQL will use the managed token instead of a password.

## Managed Identity Support

You can also use Azure Managed Identity (system-assigned or user-assigned) instead of federated identity.

In your `appsettings.json`, enable managed identity:

```json
{
  "Neolution.AzureSqlFederatedIdentity": {
    "UseManagedIdentity": true,
    "ManagedIdentityClientId": "<optional-user-assigned-client-id>"
  }
}
```

If you omit `ManagedIdentityClientId`, the system-assigned managed identity will be used.

## License

MIT

## Contributing

Contributions are welcome! Please open issues or pull requests.
