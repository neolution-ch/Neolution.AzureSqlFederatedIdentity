# Csag.AzureSqlFederatedIdentity Solution

[![Build Status](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/actions/workflows/ci.yml/badge.svg)](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/actions)
[![NuGet](https://img.shields.io/nuget/v/Csag.AzureSqlFederatedIdentity.svg)](https://www.nuget.org/packages/Csag.AzureSqlFederatedIdentity)
[![License: MIT](https://img.shields.io/badge/License-MIT-lightgray.svg)](LICENSE)

This repository provides federated identity integration for Azure SQL using Google Cloud IAM Credentials and Microsoft Entra ID (Azure AD).

## Projects

- **Csag.AzureSqlFederatedIdentity**: The main library, distributed as a [NuGet package](https://www.nuget.org/packages/Csag.AzureSqlFederatedIdentity).
- **Csag.AzureSqlFederatedIdentity.Demo**: Example ASP.NET Core application demonstrating usage.
- **Csag.AzureSqlFederatedIdentity.UnitTests**: Unit tests for the library.

## Quick Start

1. Install the NuGet package in your project:

   ```shell
   dotnet add package Csag.AzureSqlFederatedIdentity
   ```

2. For detailed cloud and identity setup instructions (Azure AD, Azure SQL, GCP, Cloud Run), see [docs/cloud-identity-setup.md](./docs/cloud-identity-setup.md).

## Release process

This repository follows the [neolution-ch release playbook](https://github.com/neolution-ch/release-playbook) (Changesets, NuGet variant):

1. Every PR that changes the library needs a changeset: run `npx changeset`, choose the bump type and describe the change. CI blocks PRs without one (`npx changeset --empty` for changes that do not affect the package).
2. On merge to `main`, the Release workflow maintains a **"chore: version packages"** PR that accumulates pending changesets; `scripts/sync-versions.mjs` keeps the `.csproj` version in step.
3. Merging that PR creates the git tag and GitHub Release; the **NuGet Publish** workflow then packs and pushes the package to nuget.org.

The package is on a `0.x` version: breaking changes are declared as **minor** changesets, fixes as **patch**. Dependabot PRs get their changesets generated automatically.

## License

This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.

## Contributing

Contributions are welcome! Please open issues or pull requests.
