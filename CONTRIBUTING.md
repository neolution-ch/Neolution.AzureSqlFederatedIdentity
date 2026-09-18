# Contributing

Thank you for helping improve Csag.WorkloadIdentity. Bug reports, questions and pull requests are welcome on [GitHub](https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity). For vulnerabilities, please follow [SECURITY.md](./SECURITY.md) instead of opening an issue.

## Prerequisites

- The .NET SDK pinned in [global.json](./global.json) (10.0.4xx; a newer 10.0 minor rolls forward), plus the **.NET 8 runtime**, because the tests also run on `net8.0`. `dotnet --list-runtimes` should list `Microsoft.NETCore.App 8.0.x` alongside 10.0.x.
- [Node.js](https://nodejs.org/) 24 for the changesets tooling: run `npm ci` once in the repository root.
- Docker, only if you want to build the Demo container (see the [Demo README](./Csag.WorkloadIdentity.Demo/README.md)).

## Build and test

```shell
dotnet restore --locked-mode        # what CI runs; fails if a packages.lock.json is out of date
dotnet build -c Release             # warnings are errors in Release, so this is the gate to pass
dotnet test -c Release              # runs the suite on net8.0 and net10.0
dotnet test -c Release -f net10.0   # a single target framework, for a quicker loop
dotnet pack Csag.WorkloadIdentity -c Release -o ./nupkgs
```

The library targets `net8.0` and `net10.0`, and CI runs the tests on both; a change is not done until both are green. Build in `Release` before you push: `TreatWarningsAsErrors` is on and the StyleCop rules from `Neolution.CodeAnalysis` are enforced there, so a build that is clean in `Debug` can still fail. Fix every warning rather than suppressing it.

## Code conventions

The analyzers enforce most of these; the rest come from the existing code.

- XML documentation on every member, public or private.
- `this.` prefix for instance members.
- `using` directives inside the namespace, sorted alphabetically with `System` namespaces first.
- Nullable reference types are enabled; `ConfigureAwait(false)` on every `await` in the library.
- Comments explain the non-obvious *why* of the code as it stands; they do not narrate edits or previous states.
- Tests use xunit, Shouldly and NSubstitute, follow the `Given_<state>_When_<action>_Then_<outcome>` naming with Arrange/Act/Assert sections, and live in `Csag.WorkloadIdentity.UnitTests`. Read an existing test class before adding one.

## Dependencies

Package versions are managed centrally with transitive pinning:

1. Add or change the version in [Directory.Packages.props](./Directory.Packages.props).
2. Reference the package in the project file without a version: `<PackageReference Include="Package.Name" />`.
3. Run `dotnet restore` (without `--locked-mode`) so that the `packages.lock.json` files update, and commit them together with the change.

Dependabot proposes routine updates and generates their changesets automatically.

## Changesets

Releases follow the [neolution-ch release playbook](https://github.com/neolution-ch/release-playbook) with [Changesets](https://github.com/changesets/changesets); the [root README](./README.md#release-process) describes the pipeline. What it means for a pull request:

- Every PR that changes the library needs a changeset file in `.changeset/`. Run `npx changeset`, pick the bump type and describe the change **for consumers of the package**; the text becomes the CHANGELOG entry.
- The package is on a `0.x` version, so the bump convention is: **minor** for a breaking or behaviour-changing change, **patch** for a fix or for documentation that ships inside the package (the package README does).
- A change that does not touch the package, such as CI, repository documentation or the Demo, still needs a changeset so that the check passes: run `npx changeset --empty`, which creates a file with an empty front matter.
- CI's **Changeset Check** fails a PR without a changeset. Do not edit `CHANGELOG.md` or the version in the `.csproj` by hand; the "chore: version packages" PR does that.

A changeset file looks like this:

```markdown
---
"@neolution-ch/csag-workload-identity": patch
---

Describe the change from the consumer's point of view.
```

## Pull requests

- Branch from `main` and keep the PR focused on one topic; small, reviewable diffs are merged faster.
- Complete the checklist in the PR template: a changeset, `dotnet build -c Release` clean, `dotnet test -c Release` green on both target frameworks, and documentation updated where behaviour or configuration changed (the package README, `docs/cloud-identity-setup.md` and the Demo README).
- Never commit secrets or real identifiers. The Demo's `appsettings.json` ships with empty values on purpose; use user secrets or environment variables locally.
- Explain *why* in the PR description; the diff shows *what*.
- A maintainer reviews every PR, and CI must be green before it is merged.
