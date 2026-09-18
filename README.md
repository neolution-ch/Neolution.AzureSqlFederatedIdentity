# Csag.WorkloadIdentity

[![Build Status](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/actions/workflows/ci.yml/badge.svg)](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/actions)
[![NuGet](https://img.shields.io/nuget/v/Csag.WorkloadIdentity.svg)](https://www.nuget.org/packages/Csag.WorkloadIdentity)
[![License: MIT](https://img.shields.io/badge/License-MIT-lightgray.svg)](./LICENSE)

A .NET library that gives an application passwordless access tokens for Azure SQL and Azure Blob Storage from the identity it already runs as. Each resource is configured on its own and obtains its token either from the **managed identity** of the Azure resource the application runs on, or from the application's **Google identity**, exchanged for a Microsoft Entra ID access token through workload identity federation. You attach the token to the `SqlConnection`, or hand it to Azure SDK clients such as `BlobServiceClient` through the library's `TokenCredential` adapter. No password, key or secret is stored anywhere.

## Projects

| Project | Description |
|---|---|
| [`Csag.WorkloadIdentity`](./Csag.WorkloadIdentity) | The library, published as the [Csag.WorkloadIdentity](https://www.nuget.org/packages/Csag.WorkloadIdentity) NuGet package for `net8.0` and `net10.0`. Its [README](./Csag.WorkloadIdentity/README.md) is the package documentation; the [CHANGELOG](./Csag.WorkloadIdentity/CHANGELOG.md) is generated from changesets. |
| [`Csag.WorkloadIdentity.Demo`](./Csag.WorkloadIdentity.Demo) | An ASP.NET Core application that reads from Azure SQL and, optionally, Blob Storage through the library, on Cloud Run or an Azure host. Its [README](./Csag.WorkloadIdentity.Demo/README.md) explains how to configure it and run it from source, in Docker and in the cloud. |
| [`Csag.WorkloadIdentity.UnitTests`](./Csag.WorkloadIdentity.UnitTests) | xunit tests for the library, run on both target frameworks. |

## Quick start

1. Set up the cloud side once. Choose the identity configuration that matches where the application runs, an Azure managed identity or Google Cloud to Azure federation, then grant that identity access to Azure SQL and Blob Storage. Every step is in [docs/cloud-identity-setup.md](./docs/cloud-identity-setup.md).
2. Install the package and wire it into your application. The [package README](./Csag.WorkloadIdentity/README.md) has two complete quickstarts, Azure SQL from Google Cloud Run and Azure SQL plus Blob Storage from Azure App Service, the reference of every configuration key, and a migration section for users of `Neolution.AzureSqlFederatedIdentity`.

   ```shell
   dotnet add package Csag.WorkloadIdentity
   ```

3. To see it end to end, run the [Demo](./Csag.WorkloadIdentity.Demo/README.md) against your own resources.

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
