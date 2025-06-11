# Neolution.AzureSqlFederatedIdentity

[![NuGet](https://img.shields.io/nuget/v/Neolution.AzureSqlFederatedIdentity.svg)](https://www.nuget.org/packages/Neolution.AzureSqlFederatedIdentity)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../LICENSE)

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
       "GoogleServiceAccountEmail": "<your-gcp-service-account-email>"
     }
   }
   ```

3. Register the services in your `Program.cs`:

   ```csharp
   builder.Services.AddAzureSqlFederatedIdentity(builder.Configuration);
   ```

## Registering Applications

For detailed steps on registering your application in Azure, configuring Azure SQL, setting up GCP, and configuring Cloud Run, see [docs/cloud-identity-setup.md](../docs/cloud-identity-setup.md).

## License

MIT

## Contributing

Contributions are welcome! Please open issues or pull requests.
