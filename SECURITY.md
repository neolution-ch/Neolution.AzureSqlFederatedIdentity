# Security policy

Csag.AzureSqlFederatedIdentity handles credentials: it obtains Google ID tokens and Microsoft Entra ID access tokens and keeps the current access token in memory. We take reports about it seriously.

## Supported versions

| Version | Supported |
|---|---|
| Latest `0.x` release on nuget.org | Yes |
| Earlier `0.x` releases | No, please upgrade to the latest release |

While the package is on a `0.x` version, security fixes are released as a new version rather than backported.

## Reporting a vulnerability

Please **do not** open a public issue or pull request for a vulnerability. Report it privately through GitHub's private vulnerability reporting for this repository:

<https://github.com/neolution-ch/Neolution.AzureSqlFederatedIdentity/security/advisories/new>

GitHub's [documentation](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability) explains how the form works. Please include:

- the package version and target framework you tested;
- a description of the issue and its impact;
- steps or code to reproduce it, if you have them.

We acknowledge every report, keep you informed while we investigate, and coordinate the disclosure and the release of a fix with you. Credit is given in the advisory unless you prefer to stay anonymous.

## Scope

- In scope: the `Csag.AzureSqlFederatedIdentity` library.
- The Demo project is a sample and is not meant for production use; reports about it are still welcome if they point to a problem in the library or its documentation.
- Vulnerabilities in dependencies such as `Azure.Identity` or the Google Cloud client libraries should be reported to their maintainers; let us know as well if the library needs to update in response.
