---
"@neolution-ch/csag-workload-identity": patch
---

Rewrite the package README as a self-contained quickstart: every configuration key including `RefreshAheadWindow` and `EnableBackgroundRefresh`, all three `AddAzureSqlFederatedIdentity` overloads, obtaining the token from `IAzureSqlTokenProvider` and handing it to `SqlClient` through a `SqlAuthenticationProvider` (also for EF Core) or `SqlConnection.AccessToken`, the prerequisites on the Google and Microsoft side, and a troubleshooting table. Links are absolute so they work on nuget.org.
