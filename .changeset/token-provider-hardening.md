---
"@neolution-ch/csag-workload-identity": minor
---

Harden token acquisition, refresh and registration.

- Concurrent callers that find no usable token share a single token exchange instead of each running their own. A failed exchange is not retained, so the next caller simply retries.
- The provider holds the current token itself; `IMemoryCache` is no longer used or registered, so hosts that configure a cache `SizeLimit` or compact their cache are unaffected. `AzureSqlTokenProvider` is now sealed and disposable, and its constructor takes a `TimeProvider` instead of an `IMemoryCache`.
- The background refresh service refreshes ahead of the token's own expiry (`ExpiresOn` minus `RefreshAheadWindow`) rather than on a fixed interval, retries with exponential backoff when an exchange fails, and stops quietly on host shutdown. It can be turned off with `EnableBackgroundRefresh = false`.
- New options: `RefreshAheadWindow` (default five minutes; must be positive) replaces the hard-coded skew and interval, and `EnableBackgroundRefresh` (default `true`). A token that arrives with less than the window remaining is still used and replaced on the next call.
- The `Google` configuration section is now required; a missing section fails validation with a clear message instead of an `ArgumentNullException` when the provider is resolved.
- Options are bound with `BindConfiguration` and validated at host startup (`ValidateOnStart`). `AddAzureSqlFederatedIdentity()` without arguments binds the `Csag.WorkloadIdentity` section from the host configuration; the section name is exposed as `AzureSqlFederatedIdentityOptions.ConfigurationSectionName`. The `Action<AzureSqlFederatedIdentityOptions>` and `IConfiguration` overloads remain.
- Services are registered with `TryAdd*`, so a consumer-registered `IAzureSqlTokenProvider` (or any other service in the pipeline) takes precedence and repeated registration calls do not duplicate services or the hosted service. A substituted provider is left alone by the background refresh service.
- The Google IAM Credentials client and the Azure `ClientAssertionCredential` are created once and reused across token exchanges.
