# Csag.AzureSqlFederatedIdentity

[![Build Status](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/actions/workflows/ci.yml/badge.svg)](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/actions)
[![NuGet](https://img.shields.io/nuget/v/Csag.AzureSqlFederatedIdentity.svg)](https://www.nuget.org/packages/Csag.AzureSqlFederatedIdentity)
[![License: MIT](https://img.shields.io/badge/License-MIT-lightgray.svg)](./LICENSE)

A .NET library that lets an application running on Google Cloud connect to Azure SQL without a password, secret or key. It exchanges the application's Google identity for a Microsoft Entra ID access token through workload identity federation (Google-signed ID token → Microsoft Entra ID access token) and you attach that token to the `SqlConnection`.

## Projects

| Project | Description |
|---|---|
| [`Csag.AzureSqlFederatedIdentity`](./Csag.AzureSqlFederatedIdentity) | The library, published as the [Csag.AzureSqlFederatedIdentity](https://www.nuget.org/packages/Csag.AzureSqlFederatedIdentity) NuGet package for `net8.0` and `net10.0`. Its [README](./Csag.AzureSqlFederatedIdentity/README.md) is the package documentation; the [CHANGELOG](./Csag.AzureSqlFederatedIdentity/CHANGELOG.md) is generated from changesets. |
| [`Csag.AzureSqlFederatedIdentity.Demo`](./Csag.AzureSqlFederatedIdentity.Demo) | An ASP.NET Core application for Cloud Run that reads from Azure SQL through the library. Its [README](./Csag.AzureSqlFederatedIdentity.Demo/README.md) explains how to run it from source, in Docker and on Cloud Run. |
| [`Csag.AzureSqlFederatedIdentity.UnitTests`](./Csag.AzureSqlFederatedIdentity.UnitTests) | xunit tests for the library, run on both target frameworks. |

## Quick start

1. Set up the cloud side once: the Google service account and its IAM role, the Microsoft Entra ID app registration with a federated credential, the Azure SQL database user and the Cloud Run runtime identity. Every step is in [docs/cloud-identity-setup.md](./docs/cloud-identity-setup.md).
2. Install the package and wire it into your application. The [package README](./Csag.AzureSqlFederatedIdentity/README.md) has the complete quickstart: configuration keys, service registration and attaching the token to `SqlConnection`, with plain ADO.NET and with EF Core.

   ```shell
   dotnet add package Csag.AzureSqlFederatedIdentity
   ```

3. To see it end to end, run the [Demo](./Csag.AzureSqlFederatedIdentity.Demo/README.md) against your own database.

## Release process

This repository follows the [neolution-ch release playbook](https://github.com/neolution-ch/release-playbook) (Changesets, NuGet variant):

1. Every PR that changes the library needs a changeset: run `npx changeset`, choose the bump type and describe the change. CI blocks PRs without one (`npx changeset --empty` for changes that do not affect the package).
2. On merge to `main`, the Release workflow maintains a **"chore: version packages"** PR that accumulates pending changesets; `scripts/sync-versions.mjs` keeps the `.csproj` version in step.
3. Merging that PR creates the git tag and GitHub Release; the **NuGet Publish** workflow then packs and pushes the package to nuget.org.

The package is on a `0.x` version: breaking changes are declared as **minor** changesets, fixes as **patch**. Dependabot PRs get their changesets generated automatically.

## Contributing

Issues and pull requests are welcome. [CONTRIBUTING.md](./CONTRIBUTING.md) describes the prerequisites, how to build and test on both target frameworks, the code conventions and what a pull request needs. Please report vulnerabilities privately as described in [SECURITY.md](./SECURITY.md).

## License

This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.
