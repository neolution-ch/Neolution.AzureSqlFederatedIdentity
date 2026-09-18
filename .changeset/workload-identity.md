---
"@neolution-ch/csag-workload-identity": minor
---

Generalise the library into a resource-first workload identity model: access tokens for Azure SQL and Azure Blob Storage, each obtained either with the Azure managed identity the application runs as or with its Google identity through workload identity federation.

- **Configuration is resource-first (breaking).** Each resource has its own section under `Csag.WorkloadIdentity` that selects a `Provider` (`ManagedIdentity` or `Google`) and carries that provider's settings. For the existing Azure SQL over Google federation case:

  Before:

  ```json
  {
    "Csag.WorkloadIdentity": {
      "TenantId": "<tenant-id>",
      "ClientId": "<client-id>",
      "Google": { "ServiceAccountEmail": "<name>@<project-id>.iam.gserviceaccount.com" }
    }
  }
  ```

  After:

  ```json
  {
    "Csag.WorkloadIdentity": {
      "AzureSql": {
        "Provider": "Google",
        "Google": {
          "TenantId": "<tenant-id>",
          "ClientId": "<client-id>",
          "ServiceAccountEmail": "<name>@<project-id>.iam.gserviceaccount.com"
        }
      }
    }
  }
  ```

  `RefreshAheadWindow` and `EnableBackgroundRefresh` stay at the root and apply to every resource. Startup validation requires at least one resource section and reports every missing value by its path, for example `AzureSql:Google:ServiceAccountEmail must be provided.`
- **Managed identity.** `"Provider": "ManagedIdentity"` with `"ManagedIdentity": { "UseSystemAssignedIdentity": true }` or `"ManagedIdentity": { "ClientId": "<user-assigned-client-id>" }` obtains the token from the managed identity endpoint of the Azure resource the application runs on.
- **Blob Storage.** A `BlobStorage` section (same shape as `AzureSql`) registers `IBlobStorageTokenProvider` with `GetBlobStorageAccessTokenAsync`, requesting the `https://storage.azure.com/.default` scope.
- **`AddWorkloadIdentity` replaces `AddAzureSqlFederatedIdentity`** with the same three overloads (host configuration, `Action<WorkloadIdentityOptions>`, `IConfiguration`). `WorkloadIdentityOptions` replaces `AzureSqlFederatedIdentityOptions`; the section name `Csag.WorkloadIdentity` is unchanged and exposed as `WorkloadIdentityOptions.ConfigurationSectionName`. Both providers are always registered; resolving the provider of a resource whose section is absent throws an `InvalidOperationException` naming the missing section.
- **`TokenCredential` adapter.** Both provider interfaces gain `GetAccessTokenAsync`, which returns the token together with its real expiry, and `WorkloadIdentityTokenCredential` presents a provider to Azure SDK clients, for example `new BlobServiceClient(uri, new WorkloadIdentityTokenCredential(blobStorageTokenProvider))`.
- `IGoogleIdTokenProvider.GetIdTokenAsync` takes the service account email, so resources can federate through different service accounts. `IAzureSqlTokenExchanger` is replaced by internal per-provider exchangers.
- Unchanged: `IAzureSqlTokenProvider.GetAzureSqlAccessTokenAsync`; the token is held in memory and concurrent callers share a single request; the background service refreshes ahead of expiry with exponential backoff, now in one loop per configured resource; every service is registered with `TryAdd`, so a consumer-registered provider wins and is left alone by the background refresh.
