---
"@neolution-ch/csag-azure-sql-federated-identity": patch
---

Rewrite the package README as a self-contained quickstart: every configuration key including `RefreshAheadWindow` and `EnableBackgroundRefresh`, all three `AddAzureSqlFederatedIdentity` overloads, obtaining the token from `IAzureSqlTokenProvider` and assigning it to `SqlConnection.AccessToken` with plain ADO.NET and with EF Core, the prerequisites on the Google and Microsoft side, and a troubleshooting table. Links are absolute so they work on nuget.org.
