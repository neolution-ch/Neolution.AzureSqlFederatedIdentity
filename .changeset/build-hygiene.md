---
"@neolution-ch/csag-azure-sql-federated-identity": minor
---

Target `net8.0` and `net10.0`, and tidy the dependency surface.

- The unused `Microsoft.Data.SqlClient` dependency is gone; attach the token to your own `SqlConnection` exactly as before.
- `Microsoft.Extensions.*` dependencies move to 10.x (still consumable from net8.0 hosts); `Azure.Identity` 1.21 and `Google.Cloud.Iam.Credentials.V1` 2.5.
- The debug-only decoding and logging of the Google ID token's claims is removed.
- Package metadata: `<Version>` is declared, deterministic/SourceLink builds are enabled, and symbols ship as `.snupkg`.
