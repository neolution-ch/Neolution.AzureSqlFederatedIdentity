# Neolution.WorkloadIdentity Solution

[![Build Status](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/actions/workflows/ci.yml/badge.svg)](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/actions)
[![NuGet](https://img.shields.io/nuget/v/Neolution.AzureSqlFederatedIdentity.svg)](https://www.nuget.org/packages/Neolution.AzureSqlFederatedIdentity)
[![License: MIT](https://img.shields.io/badge/License-MIT-lightgray.svg)](LICENSE)

This repository provides federated and passwordless identity integration for Azure SQL and Azure Blob Storage using Microsoft Entra ID (Azure AD) and Google Cloud IAM Credentials. It supports both Azure Managed Identity and Google Cloud Workload Identity Federation, enabling secure, secretless access from Azure or Google Cloud environments.

## Projects

- **Neolution.WorkloadIdentity**: The main library, distributed as a [NuGet package](https://www.nuget.org/packages/Neolution.AzureSqlFederatedIdentity).
- **Neolution.WorkloadIdentity.Demo**: Example ASP.NET Core application demonstrating usage.
- **Neolution.WorkloadIdentity.UnitTests**: Unit tests for the library.

## Quick Start

1. Install the NuGet package in your project:

   ```shell
   dotnet add package Neolution.AzureSqlFederatedIdentity
   ```

2. For detailed cloud and identity setup instructions (Azure AD, Azure SQL, GCP, Cloud Run), see [IDENTITY-SETUP.md](IDENTITY-SETUP.md).

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please open issues or pull requests.
