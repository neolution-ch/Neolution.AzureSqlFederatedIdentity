# Neolution.AzureSqlFederatedIdentity Solution

[![Build Status](https://github.com/your-org/Neolution.AzureSqlFederatedIdentity/actions/workflows/build.yml/badge.svg)](https://github.com/your-org/Neolution.AzureSqlFederatedIdentity/actions)
[![NuGet](https://img.shields.io/nuget/v/Neolution.AzureSqlFederatedIdentity.svg)](https://www.nuget.org/packages/Neolution.AzureSqlFederatedIdentity)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

This repository provides federated identity integration for Azure SQL using Google Cloud IAM Credentials and Microsoft Entra ID (Azure AD).

## Projects

- **Neolution.AzureSqlFederatedIdentity**: The main library, distributed as a [NuGet package](https://www.nuget.org/packages/Neolution.AzureSqlFederatedIdentity).
- **Neolution.AzureSqlFederatedIdentity.Sample**: Example ASP.NET Core application demonstrating usage.
- **Neolution.AzureSqlFederatedIdentity.UnitTests**: Unit tests for the library.

## Quick Start

1. Install the NuGet package in your project:

   ```shell
   dotnet add package Neolution.AzureSqlFederatedIdentity
   ```

2. For detailed cloud and identity setup instructions (Azure AD, Azure SQL, GCP, Cloud Run), see [docs/cloud-identity-setup.md](./docs/cloud-identity-setup.md).

## License

This project is licensed under the MIT License. See the [LICENSE](./Neolution.AzureSqlFederatedIdentity/LICENSE) file for details.

## Contributing

Contributions are welcome! Please open issues or pull requests.
