---
"@neolution-ch/csag-workload-identity": patch
---

Correct the package README and the cloud setup guide.

- The `SqlConnection.AccessToken` sample now reads its own connection string, `ConnectionStrings:AzureSqlAccessToken`, which has no `Authentication` keyword. `SqlClient` throws an `InvalidOperationException` when you set `AccessToken` on a connection string that has this keyword.
- `SqlConnection.AccessTokenCallback` keeps a single pool only when all connections get the same delegate. The README now tells you to create the delegate one time.
- The README now shows the two overloads under "Registering with a given configuration or in code" as alternatives.
- The migration section now tells you that `Csag.WorkloadIdentity` does not reference `Microsoft.Data.SqlClient` and does not register `IMemoryCache`. `Neolution.AzureSqlFederatedIdentity` did both.
- The example of object-level grants in the setup guide no longer grants `EXECUTE` on the full `dbo` schema. The setup guide also quotes error 33134, which Azure SQL reports when the server identity is missing.
