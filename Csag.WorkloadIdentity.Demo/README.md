# Csag.WorkloadIdentity.Demo

A minimal ASP.NET Core app that reads a table from Azure SQL and, optionally, a file from Azure Blob Storage without a stored credential. The `Csag.WorkloadIdentity` library obtains the access tokens with the app's workload identity: on Google Cloud Run that is the app's Google service account, exchanged for a Microsoft Entra ID token through workload identity federation; on an Azure host it is the resource's managed identity. [`Database/AppDbContextFactory.cs`](Database/AppDbContextFactory.cs) attaches the Azure SQL token to each `SqlConnection`, and [`Extensions/BlobStorageExtensions.cs`](Extensions/BlobStorageExtensions.cs) builds the `BlobServiceClient` on the library's `WorkloadIdentityTokenCredential`.

| Route | Behaviour |
|---|---|
| `GET /` | Greeting; proves the container is up. |
| `GET /test` | Counts and lists the rows of `dbo.TestTable`, then downloads the test blob: `200` with `{ "count": n, "rows": [...], "blobContent": "...", "blobSkipped": false }`, `404` if the table is empty, `500` (generic problem response) if a token request, the SQL connection or the download fails. The reason is in the application log. Without the optional Blob Storage settings the response carries `"blobContent": null, "blobSkipped": true`. |

## Prerequisites

The main path is Google Cloud Run with the `Google` provider:

1. The cloud side described in [docs/cloud-identity-setup.md](../docs/cloud-identity-setup.md): a Google service account, a Microsoft Entra app registration with a federated credential for it, and a database user for that app registration.
2. Google Application Default Credentials (ADC), which the library uses to ask Google for an ID token for the service account:
   - **On Cloud Run** there is nothing to configure: set the service's runtime service account to the configured service account (it needs `roles/iam.serviceAccountOpenIdTokenCreator` on itself).
   - **On a workstation** run `gcloud auth application-default login`. Your Google account needs `roles/iam.serviceAccountOpenIdTokenCreator` on the service account (granted on the service account, not on the project), and the IAM Service Account Credentials API must be enabled in the project (`gcloud services enable iamcredentials.googleapis.com`).
3. The .NET SDK pinned in [global.json](../global.json), and Docker if you want to run the container.

An app that runs on Azure needs none of the Google side; see [Managed identity on an Azure host](#managed-identity-on-an-azure-host).

## Settings

The library binds the `Csag.WorkloadIdentity` section, which has one sub-section per resource. `appsettings.json` selects the `Google` provider for `AzureSql` and ships with the values empty. Provide them through user secrets on a workstation or environment variables in a container (`__` stands for the `:` separator; the `.` in the section name is part of the variable name):

| Setting | User-secrets / JSON key | Environment variable |
|---|---|---|
| Identity provider for Azure SQL (`Google`, set in `appsettings.json`) | `Csag.WorkloadIdentity:AzureSql:Provider` | `Csag.WorkloadIdentity__AzureSql__Provider` |
| Microsoft Entra tenant ID | `Csag.WorkloadIdentity:AzureSql:Google:TenantId` | `Csag.WorkloadIdentity__AzureSql__Google__TenantId` |
| Application (client) ID of the app registration | `Csag.WorkloadIdentity:AzureSql:Google:ClientId` | `Csag.WorkloadIdentity__AzureSql__Google__ClientId` |
| Google service account email | `Csag.WorkloadIdentity:AzureSql:Google:ServiceAccountEmail` | `Csag.WorkloadIdentity__AzureSql__Google__ServiceAccountEmail` |
| Azure SQL connection string | `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |

The three Google values are validated at startup, and the app refuses to start if any of them is missing. The connection string is checked on the first request to `/test`.

```shell
dotnet user-secrets set "Csag.WorkloadIdentity:AzureSql:Google:TenantId" "<tenant-id>" --project Csag.WorkloadIdentity.Demo
dotnet user-secrets set "Csag.WorkloadIdentity:AzureSql:Google:ClientId" "<client-id>" --project Csag.WorkloadIdentity.Demo
dotnet user-secrets set "Csag.WorkloadIdentity:AzureSql:Google:ServiceAccountEmail" "<name>@<project>.iam.gserviceaccount.com" --project Csag.WorkloadIdentity.Demo
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True" --project Csag.WorkloadIdentity.Demo
```

The connection string must not contain `User ID`, `Password`, `Integrated Security` or `Authentication`: the access token is the credential, and `SqlClient` rejects a connection string that also carries one of those.

### Optional: Blob Storage test file

To make `/test` also download a blob, configure a `BlobStorage` resource (the commented block in `appsettings.json` shows the shape) and name the file:

| Setting | User-secrets / JSON key | Environment variable |
|---|---|---|
| Identity provider for Blob Storage | `Csag.WorkloadIdentity:BlobStorage:Provider` | `Csag.WorkloadIdentity__BlobStorage__Provider` |
| Tenant ID, client ID and service account email, as for Azure SQL | `Csag.WorkloadIdentity:BlobStorage:Google:TenantId`, `...:ClientId`, `...:ServiceAccountEmail` | `Csag.WorkloadIdentity__BlobStorage__Google__TenantId`, `...__ClientId`, `...__ServiceAccountEmail` |
| Blob service endpoint of the storage account | `BlobStorageTestFile:Endpoint` | `BlobStorageTestFile__Endpoint` |
| Test file as `<container>/<blob>` | `BlobStorageTestFile:FilePath` | `BlobStorageTestFile__FilePath` |

The same app registration can serve both resources: repeat the three Google values under `BlobStorage` and assign its service principal the **Storage Blob Data Reader** role on the container or the storage account. The endpoint is `https://<account>.blob.core.windows.net`.

```shell
dotnet user-secrets set "Csag.WorkloadIdentity:BlobStorage:Provider" "Google" --project Csag.WorkloadIdentity.Demo
dotnet user-secrets set "Csag.WorkloadIdentity:BlobStorage:Google:TenantId" "<tenant-id>" --project Csag.WorkloadIdentity.Demo
dotnet user-secrets set "Csag.WorkloadIdentity:BlobStorage:Google:ClientId" "<client-id>" --project Csag.WorkloadIdentity.Demo
dotnet user-secrets set "Csag.WorkloadIdentity:BlobStorage:Google:ServiceAccountEmail" "<name>@<project>.iam.gserviceaccount.com" --project Csag.WorkloadIdentity.Demo
dotnet user-secrets set "BlobStorageTestFile:Endpoint" "https://<account>.blob.core.windows.net" --project Csag.WorkloadIdentity.Demo
dotnet user-secrets set "BlobStorageTestFile:FilePath" "<container>/<blob>" --project Csag.WorkloadIdentity.Demo
```

Blob Storage is optional: when the `BlobStorage` section or either `BlobStorageTestFile` value is missing, the app does not register the Blob Storage client and `/test` returns the SQL rows with `"blobSkipped": true`. A configured `BlobStorage` section is validated at startup like `AzureSql`.

### Managed identity on an Azure host

When the Demo runs on an Azure resource (App Service, Container Apps, AKS, a VM), it can obtain the tokens from that resource's managed identity instead; there is then no Google side and no ADC. Select the `ManagedIdentity` provider per resource, and either the system-assigned identity or a user-assigned one by client ID:

```shell
Csag.WorkloadIdentity__AzureSql__Provider=ManagedIdentity
Csag.WorkloadIdentity__AzureSql__ManagedIdentity__UseSystemAssignedIdentity=true
```

or, for a user-assigned identity, `Csag.WorkloadIdentity__AzureSql__ManagedIdentity__ClientId=<client-id>` instead of the second line. The `Google` values in `appsettings.json` are ignored for a resource whose provider is `ManagedIdentity`. Create the database user for the managed identity (`CREATE USER [<identity-name>] FROM EXTERNAL PROVIDER;` with the same roles as in the setup guide) and, for the optional blob test, configure `BlobStorage` the same way and assign the identity **Storage Blob Data Reader**.

## Schema

The app never creates schema. The identity it runs as only has `db_datareader` and `db_datawriter` (see the setup guide), so create the table once as the server's Microsoft Entra admin or another login with DDL rights:

```sql
CREATE TABLE dbo.TestTable
(
    Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_TestTable PRIMARY KEY,
    Value NVARCHAR(MAX) NOT NULL
);

INSERT INTO dbo.TestTable (Value) VALUES (N'hello'), (N'world');
```

## Run from source

```shell
dotnet run --project Csag.WorkloadIdentity.Demo
```

The application stays in the foreground; request the endpoint from a second terminal:

```shell
curl http://localhost:5172/test
```

## Run in Docker

The image is built from the **repository root**, because the Demo references the library project:

```shell
docker build -f Csag.WorkloadIdentity.Demo/Dockerfile -t csag-demo .
```

The container listens on port 8080 and runs as the non-root `app` user (uid 1654). Outside Cloud Run it has no ADC of its own, so mount your workstation's credential file and point the Google SDK at it. `gcloud` creates that file readable by your user only, so for this local test run the container as your own user (`--user`), which overrides the image's `app` user; on Windows the mounted file is readable regardless and `--user` can be left out. Cloud Run needs none of this:

```shell
docker run --rm -p 8080:8080 --user "$(id -u):$(id -g)" \
  -e Csag.WorkloadIdentity__AzureSql__Provider=Google \
  -e Csag.WorkloadIdentity__AzureSql__Google__TenantId="<tenant-id>" \
  -e Csag.WorkloadIdentity__AzureSql__Google__ClientId="<client-id>" \
  -e Csag.WorkloadIdentity__AzureSql__Google__ServiceAccountEmail="<name>@<project>.iam.gserviceaccount.com" \
  -e ConnectionStrings__DefaultConnection="Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True" \
  -v "$HOME/.config/gcloud/application_default_credentials.json:/adc.json:ro" \
  -e GOOGLE_APPLICATION_CREDENTIALS=/adc.json \
  csag-demo
```

The container stays in the foreground as well; from a second terminal:

```shell
curl http://localhost:8080/test
```

On Windows the credential file is `%APPDATA%\gcloud\application_default_credentials.json`. Add the `Csag.WorkloadIdentity__BlobStorage__*` and `BlobStorageTestFile__*` variables from the table above to include the blob test.

To check only that the image starts, run it with placeholder values (the library validates their presence, not their meaning) and request `/`, as the CI workflow does:

```shell
docker run -d -p 8080:8080 --name csag-demo \
  -e Csag.WorkloadIdentity__AzureSql__Provider=Google \
  -e Csag.WorkloadIdentity__AzureSql__Google__TenantId=placeholder \
  -e Csag.WorkloadIdentity__AzureSql__Google__ClientId=placeholder \
  -e Csag.WorkloadIdentity__AzureSql__Google__ServiceAccountEmail=placeholder@example.invalid \
  csag-demo
curl http://localhost:8080/
docker stop csag-demo
```

## Deploy to Cloud Run

Push the image to Artifact Registry and deploy it with the runtime service account set to the configured Google service account and the settings from the tables above supplied as environment variables (`Csag.WorkloadIdentity__AzureSql__Provider=Google`, the three `Csag.WorkloadIdentity__AzureSql__Google__*` values and `ConnectionStrings__DefaultConnection`, plus the Blob Storage variables if you want the blob test). Cloud Run sends traffic to port 8080, which is the port the image listens on. The [setup guide](../docs/cloud-identity-setup.md) covers the Cloud Run configuration in detail.
