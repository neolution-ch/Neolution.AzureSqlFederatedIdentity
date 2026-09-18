# Csag.AzureSqlFederatedIdentity.Demo

A minimal ASP.NET Core app that runs on Google Cloud Run and reads from Azure SQL without a database password. The `Csag.AzureSqlFederatedIdentity` library turns the app's Google identity into an Azure AD access token, and [`Database/AppDbContextFactory.cs`](Database/AppDbContextFactory.cs) attaches that token to each `SqlConnection`.

| Route | Behaviour |
|---|---|
| `GET /` | Greeting; proves the container is up. |
| `GET /test` | Counts and lists the rows of `dbo.TestTable`: `200` with `{ "count": n, "rows": [...] }`, `404` if the table is empty, `500` (generic problem response) if the token exchange or the SQL connection fails. The reason is in the application log. |

## Prerequisites

1. The cloud side described in [docs/cloud-identity-setup.md](../docs/cloud-identity-setup.md): a Google service account, an Azure AD app registration with a federated credential for it, and a database user for that app registration.
2. Google Application Default Credentials (ADC), which the library uses to ask Google for an ID token for the service account:
   - **On Cloud Run** there is nothing to configure: set the service's runtime service account to the configured service account (it needs `roles/iam.serviceAccountOpenIdTokenCreator` on itself).
   - **On a workstation** run `gcloud auth application-default login`. Your Google account needs `roles/iam.serviceAccountOpenIdTokenCreator` on the service account (granted on the service account, not on the project), and the IAM Service Account Credentials API must be enabled in the project (`gcloud services enable iamcredentials.googleapis.com`).
3. The .NET SDK pinned in [global.json](../global.json), and Docker if you want to run the container.

## Settings

`appsettings.json` ships with the four values empty. Provide them through user secrets on a workstation or environment variables in a container:

| Setting | User-secrets / JSON key | Environment variable |
|---|---|---|
| Azure AD tenant ID | `Csag.AzureSqlFederatedIdentity:TenantId` | `Csag.AzureSqlFederatedIdentity__TenantId` |
| Azure AD application (client) ID | `Csag.AzureSqlFederatedIdentity:ClientId` | `Csag.AzureSqlFederatedIdentity__ClientId` |
| Google service account email | `Csag.AzureSqlFederatedIdentity:Google:ServiceAccountEmail` | `Csag.AzureSqlFederatedIdentity__Google__ServiceAccountEmail` |
| Azure SQL connection string | `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |

The first three are validated at startup, and the app refuses to start if any of them is missing. The connection string is checked on the first request to `/test`.

```shell
dotnet user-secrets set "Csag.AzureSqlFederatedIdentity:TenantId" "<tenant-id>" --project Csag.AzureSqlFederatedIdentity.Demo
dotnet user-secrets set "Csag.AzureSqlFederatedIdentity:ClientId" "<client-id>" --project Csag.AzureSqlFederatedIdentity.Demo
dotnet user-secrets set "Csag.AzureSqlFederatedIdentity:Google:ServiceAccountEmail" "<name>@<project>.iam.gserviceaccount.com" --project Csag.AzureSqlFederatedIdentity.Demo
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True" --project Csag.AzureSqlFederatedIdentity.Demo
```

The connection string must not contain `User ID`, `Password`, `Integrated Security` or `Authentication`: the access token is the credential, and `SqlClient` rejects a connection string that also carries one of those.

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
dotnet run --project Csag.AzureSqlFederatedIdentity.Demo
```

The application stays in the foreground; request the endpoint from a second terminal:

```shell
curl http://localhost:5172/test
```

## Run in Docker

The image is built from the **repository root**, because the Demo references the library project:

```shell
docker build -f Csag.AzureSqlFederatedIdentity.Demo/Dockerfile -t csag-demo .
```

The container listens on port 8080 and runs as the non-root `app` user (uid 1654). Outside Cloud Run it has no ADC of its own, so mount your workstation's credential file and point the Google SDK at it. `gcloud` creates that file readable by your user only, so for this local test run the container as your own user (`--user`), which overrides the image's `app` user; on Windows the mounted file is readable regardless and `--user` can be left out. Cloud Run needs none of this:

```shell
docker run --rm -p 8080:8080 --user "$(id -u):$(id -g)" \
  -e Csag.AzureSqlFederatedIdentity__TenantId="<tenant-id>" \
  -e Csag.AzureSqlFederatedIdentity__ClientId="<client-id>" \
  -e Csag.AzureSqlFederatedIdentity__Google__ServiceAccountEmail="<name>@<project>.iam.gserviceaccount.com" \
  -e ConnectionStrings__DefaultConnection="Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True" \
  -v "$HOME/.config/gcloud/application_default_credentials.json:/adc.json:ro" \
  -e GOOGLE_APPLICATION_CREDENTIALS=/adc.json \
  csag-demo
```

The container stays in the foreground as well; from a second terminal:

```shell
curl http://localhost:8080/test
```

On Windows the credential file is `%APPDATA%\gcloud\application_default_credentials.json`.

## Deploy to Cloud Run

Push the image to Artifact Registry and deploy it with the runtime service account set to the configured Google service account and the four settings supplied as environment variables. Cloud Run sends traffic to port 8080, which is the port the image listens on. Section 4 of the [setup guide](../docs/cloud-identity-setup.md) has the details.
