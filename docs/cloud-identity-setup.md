# Cloud and identity setup

This guide sets up everything outside your code so that an application on Google Cloud can connect to Azure SQL through `Csag.WorkloadIdentity`: the Google Cloud service account and its permissions, the app registration and federated credential in Microsoft Entra ID (formerly Azure Active Directory), the database user in Azure SQL, the application's configuration and the Cloud Run deployment. For the code that uses the token, see the [package README](../Csag.WorkloadIdentity/README.md).

## How the pieces fit

```text
Your application, running as a Google service account (Application Default Credentials)
   │
   │  1. generateIdToken(audience = "api://AzureADTokenExchange")      IAM Service Account Credentials API
   ▼
Google-signed ID token   iss = https://accounts.google.com, sub = <service account unique ID>
   │
   │  2. client assertion for app registration <ClientId> in tenant <TenantId>      Microsoft Entra ID
   ▼
Access token for https://database.windows.net/.default
   │
   │  3. SqlConnection.AccessToken
   ▼
Azure SQL, as the database user created for the app registration
```

Values you collect along the way:

| Value | Comes from | Used as |
|---|---|---|
| Service account email | Google Cloud, step 1.2 | `Google:ServiceAccountEmail` (step 4) |
| Service account unique ID | Google Cloud, step 1.2 | Subject of the federated credential (step 2.2) |
| Directory (tenant) ID | Microsoft Entra ID, step 2.1 | `TenantId` (step 4) |
| Application (client) ID | Microsoft Entra ID, step 2.1 | `ClientId` (step 4) |
| App registration display name | Microsoft Entra ID, step 2.1 | Database user name (step 3.2) |

## 1. Google Cloud

### 1.1 Enable the IAM Service Account Credentials API

The library mints the ID token by calling `generateIdToken` on this API. It must be enabled in the project that owns the service account, otherwise every call fails with `PERMISSION_DENIED`:

```shell
gcloud services enable iamcredentials.googleapis.com --project <project-id>
```

### 1.2 Create the service account

```shell
gcloud iam service-accounts create <name> --project <project-id> --display-name "<display name>"
```

The service account's email is `<name>@<project-id>.iam.gserviceaccount.com`. Also note its numeric **unique ID**, which the federated credential in step 2.2 uses as the subject:

```shell
gcloud iam service-accounts describe <name>@<project-id>.iam.gserviceaccount.com --format 'value(uniqueId)'
```

The Google Cloud console shows both values on the service account's details page under **IAM & Admin** > **Service Accounts**.

### 1.3 Grant the least privilege needed

The only IAM operation the library performs is `generateIdToken`, which requires the single permission `iam.serviceAccounts.getOpenIdToken` on the service account. The predefined role that contains exactly that permission is **Service Account OpenID Connect Identity Token Creator** (`roles/iam.serviceAccountOpenIdTokenCreator`). Grant it to the identity the application runs as, **on the service account resource itself**:

```shell
gcloud iam service-accounts add-iam-policy-binding <name>@<project-id>.iam.gserviceaccount.com \
  --member "serviceAccount:<name>@<project-id>.iam.gserviceaccount.com" \
  --role roles/iam.serviceAccountOpenIdTokenCreator
```

The member here is the service account itself, which is the intended deployment: the Cloud Run service runs as this account (step 5) and mints ID tokens for it. Google's Service Account Credentials API allows this; its [self-impersonation](https://cloud.google.com/iam/docs/service-account-creds) restriction covers generating access tokens and signing, not ID tokens. A developer who runs the application on a workstation needs the same role on the service account for their own account:

```shell
gcloud iam service-accounts add-iam-policy-binding <name>@<project-id>.iam.gserviceaccount.com \
  --member "user:<developer>@<domain>" \
  --role roles/iam.serviceAccountOpenIdTokenCreator
```

Why this binding and not a broader one:

- **Bind on the service account, not on the project.** A project-level binding (`gcloud projects add-iam-policy-binding`) applies to every service account in the project, including ones created later, so the member could mint ID tokens for all of them. A binding on the service account limits the grant to that one account, which is what Google recommends for [direct short-lived credentials](https://cloud.google.com/iam/docs/create-short-lived-credentials-direct).
- **Prefer the OpenID Token Creator role to Service Account Token Creator.** `roles/iam.serviceAccountTokenCreator` additionally grants `iam.serviceAccounts.getAccessToken`, `signBlob`, `signJwt` and `implicitDelegation`, that is, full impersonation of the account. The library needs none of those.

### 1.4 Application Default Credentials

The library never reads a key file of its own. It uses [Application Default Credentials](https://cloud.google.com/docs/authentication/application-default-credentials) (ADC), which the Google client library resolves from the `GOOGLE_APPLICATION_CREDENTIALS` environment variable, then from the credential file written by `gcloud auth application-default login`, and finally from the service account attached to the Cloud Run, GKE or Compute Engine resource.

- On Cloud Run nothing needs configuring beyond the runtime service account (step 5).
- On a workstation run `gcloud auth application-default login` once. The application then acts as your user account, which needs the role from step 1.3.
- Do not create a service account key; nothing in this setup requires one.

## 2. Microsoft Entra ID

### 2.1 Register an application

1. In the [Azure portal](https://portal.azure.com/) go to **Microsoft Entra ID** > **App registrations** > **New registration**.
2. Enter a name. "Accounts in this organizational directory only" is sufficient, and no redirect URI is needed.
3. On the registration's **Overview** page copy the **Application (client) ID** and the **Directory (tenant) ID**, and note the display name.

Do not create a client secret or certificate: the federated credential in the next step replaces them.

### 2.2 Add a federated credential

Under the registration go to **Certificates & secrets** > **Federated credentials** > **Add credential** and choose the **Other issuer** scenario:

| Field | Value |
|---|---|
| Issuer | `https://accounts.google.com` |
| Subject identifier | The service account's **unique ID** from step 1.2 (the numeric ID, not the email) |
| Audience | `api://AzureADTokenExchange` (the default) |
| Name | Any name, for example the service account's name |

The audience is fixed: the library requests every Google ID token with exactly `api://AzureADTokenExchange`, which is the value Microsoft Entra ID recommends for workload identity federation. It is not the app registration's client ID and not the Azure SQL resource. Microsoft Entra ID fetches Google's signing keys through the issuer, verifies the ID token's signature, and accepts it as a client assertion only if its `sub` and `aud` claims match this credential.

## 3. Azure SQL

### 3.1 Set a Microsoft Entra admin for the server

In the Azure portal open the logical SQL server and, under **Settings** > **Microsoft Entra ID**, set an admin if none is set. Only a Microsoft Entra identity can create database users from an external provider, so connect to the database as that admin (for example with SQL Server Management Studio, Azure Data Studio or `sqlcmd` using Microsoft Entra authentication) to run the statements below.

### 3.2 Create a database user for the app registration

In the target database (not in `master`), create a contained user for the app registration. The user represents the app registration; the Google service account never appears in Azure SQL:

Creating a user for a service principal, which is what an app registration or a managed identity is, needs one more thing than creating one for a person: Azure SQL cannot look the principal up with the connected admin's permissions, so the SQL engine uses the *server identity*, the managed identity assigned to the logical server, to query Microsoft Graph. Assign the server an identity (in the portal under the server's **Identity** page, or `az sql server update --resource-group <resource-group> --name <server> --assign-identity`) and grant that identity permission to read the directory: add it to the Microsoft Entra **Directory Readers** role, or grant it the Microsoft Graph application permissions `User.Read.All`, `GroupMember.Read.All` and `Application.Read.All`. Without this, the statement below fails with "Principal '…' could not be found or this principal type is not supported". See [Microsoft Entra service principals with Azure SQL](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-aad-service-principal).

```sql
CREATE USER [<app-registration-display-name>] FROM EXTERNAL PROVIDER;
```

Then grant permissions. The built-in roles are a convenient starting point:

```sql
ALTER ROLE db_datareader ADD MEMBER [<app-registration-display-name>];
ALTER ROLE db_datawriter ADD MEMBER [<app-registration-display-name>];
```

`db_datareader` and `db_datawriter` allow reading and writing every table in the database. Narrow that to what the application actually needs: a read-only service needs only `db_datareader`, and explicit grants scope access to particular objects, for example:

```sql
GRANT SELECT, INSERT ON dbo.Orders TO [<app-registration-display-name>];
GRANT EXECUTE ON SCHEMA::dbo TO [<app-registration-display-name>];
```

Neither built-in role allows schema changes; run migrations with a separate, more privileged identity rather than widening this one.

### 3.3 Allow the network path

The server's [network access controls](https://learn.microsoft.com/en-us/azure/azure-sql/database/network-access-controls-overview) must admit connections from where the application runs. Cloud Run's outbound IP addresses are not fixed by default, so to use IP firewall rules give the service a [static outbound IP address](https://cloud.google.com/run/docs/configuring/static-outbound-ip), or use private connectivity instead.

## 4. Configure the application

The library binds its options from the `Csag.WorkloadIdentity` configuration section. These are the exact keys, with the environment variable form that .NET maps to the same keys (`__` stands for the `:` separator; the `.` in the section name is part of the variable name):

| Configuration key | Environment variable | Value |
|---|---|---|
| `Csag.WorkloadIdentity:TenantId` | `Csag.WorkloadIdentity__TenantId` | Directory (tenant) ID (step 2.1) |
| `Csag.WorkloadIdentity:ClientId` | `Csag.WorkloadIdentity__ClientId` | Application (client) ID (step 2.1) |
| `Csag.WorkloadIdentity:Google:ServiceAccountEmail` | `Csag.WorkloadIdentity__Google__ServiceAccountEmail` | Service account email (step 1.2) |
| `Csag.WorkloadIdentity:RefreshAheadWindow` | `Csag.WorkloadIdentity__RefreshAheadWindow` | Optional; how long before expiry the token is refreshed. Default `00:05:00`, must be positive |
| `Csag.WorkloadIdentity:EnableBackgroundRefresh` | `Csag.WorkloadIdentity__EnableBackgroundRefresh` | Optional; default `true` |

The first three are required; the application refuses to start if any of them is missing. In `appsettings.json`:

```json
{
  "Csag.WorkloadIdentity": {
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id>",
    "Google": {
      "ServiceAccountEmail": "<name>@<project-id>.iam.gserviceaccount.com"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True"
  }
}
```

The connection string is your own application's setting (the Demo reads `ConnectionStrings:DefaultConnection`). It names only the server and database: the access token is the credential, so it must not contain `User ID`/`Password`, `Integrated Security` or an `Authentication` keyword, and `Encrypt=True` keeps the token and the data on TLS. The [package README](../Csag.WorkloadIdentity/README.md) shows how to register the library and attach the token to a `SqlConnection`.

## 5. Cloud Run

Deploy the application with the service account from step 1 as its runtime identity, so that ADC resolves to that account, and supply the settings as environment variables. A YAML file keeps the connection string's commas out of the command line:

```yaml
# env.yaml
Csag.WorkloadIdentity__TenantId: "<tenant-id>"
Csag.WorkloadIdentity__ClientId: "<client-id>"
Csag.WorkloadIdentity__Google__ServiceAccountEmail: "<name>@<project-id>.iam.gserviceaccount.com"
ConnectionStrings__DefaultConnection: "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True"
```

```shell
gcloud run deploy <service> \
  --project <project-id> \
  --region <region> \
  --image <region>-docker.pkg.dev/<project-id>/<repository>/<image>:<tag> \
  --service-account <name>@<project-id>.iam.gserviceaccount.com \
  --env-vars-file env.yaml
```

To change only the identity of an existing service, use `gcloud run services update <service> --service-account <name>@<project-id>.iam.gserviceaccount.com`. In the console, the runtime service account is under the service's **Security** tab and the variables under **Variables & Secrets**.

At startup the application validates its configuration: the three required settings must be present and `RefreshAheadWindow` must be positive. With background refresh on (the default) the first token exchange happens right after startup; with it off, on the first database access. Either way the application requests an ID token for the configured service account through ADC (as the runtime service account), exchanges it at Microsoft Entra ID, and opens the connection with the resulting access token. If something fails, the application log names the failing step; the troubleshooting table in the [package README](../Csag.WorkloadIdentity/README.md) maps the usual messages to their cause.

## References

- Google Cloud: [Create short-lived credentials for a service account](https://cloud.google.com/iam/docs/create-short-lived-credentials-direct), [Service account credentials and self-impersonation](https://cloud.google.com/iam/docs/service-account-creds), [IAM roles and permissions](https://cloud.google.com/iam/docs/roles-permissions/iam), [`generateIdToken`](https://cloud.google.com/iam/docs/reference/credentials/rest/v1/projects.serviceAccounts/generateIdToken), [Application Default Credentials](https://cloud.google.com/docs/authentication/application-default-credentials), [Cloud Run service identity](https://cloud.google.com/run/docs/configuring/services/service-identity), [Cloud Run environment variables](https://cloud.google.com/run/docs/configuring/services/environment-variables)
- Microsoft: [Workload identity federation](https://learn.microsoft.com/en-us/entra/workload-id/workload-identity-federation), [Configure an app to trust an external identity provider](https://learn.microsoft.com/en-us/entra/workload-id/workload-identity-federation-create-trust), [Microsoft Entra authentication for Azure SQL](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-aad-overview), [Azure SQL with a service principal](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-aad-service-principal), [Database-level roles](https://learn.microsoft.com/en-us/sql/relational-databases/security/authentication-access/database-level-roles), [`SqlConnection.AccessToken`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.accesstoken)
