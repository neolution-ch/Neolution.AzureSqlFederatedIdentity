# Cloud and Identity Setup Guide

This guide explains how to register your application in Microsoft Entra (Azure AD), configure Azure SQL, set up Google Cloud Platform (GCP) service accounts, and configure Cloud Run for federated identity scenarios.

## 1. Setting Up Google Cloud Platform (GCP)

1. Go to the [Google Cloud Console](https://console.cloud.google.com/).
2. Navigate to **IAM & Admin** > **Service Accounts**.
3. Create a new Service Account or select an existing one.
4. Note the **Email** of the service account; this will be used in your application's configuration (e.g., `ServiceAccountEmail` setting for the `Neolution.AzureSqlFederatedIdentity` library).
5. Find the **Unique ID** of the service account. You can find this by:
   * Clicking on the service account in the list.
   * It's often displayed on the details page, or you can get it using the gcloud CLI: `gcloud iam service-accounts describe <service-account-email> --format='value(uniqueId)'`.
   This **Unique ID** is what you will need for the "Subject identifier" when setting up the federated credential in Azure AD (described in Section 2).
6. Grant this service account the **Service Account Token Creator** role (`roles/iam.serviceAccountTokenCreator`). This allows it to generate ID tokens for itself.
7. (Optional) Restrict the service account's other permissions as needed for security following the principle of least privilege.
8. When running your application on Google Cloud (e.g., Cloud Run, GKE, Compute Engine), ensure Application Default Credentials (ADC) are configured. Typically, this means the environment is set up to allow the application to acquire credentials automatically without needing a service account JSON key file. For local development, you might need to configure ADC using `gcloud auth application-default login`.

## 2. Registering an Application in Microsoft Entra (Azure AD)

1. Go to the [Azure Portal](https://portal.azure.com/).
2. Navigate to **Microsoft Entra ID** > **Manage** > **App registrations** > **New registration**.
3. Enter a name, select supported account types, and register.
4. Copy the **Application (client) ID** and **Directory (tenant) ID** for configuration.
5. Under the registered application, navigate to **Manage** > **Certificates & secrets** > **Federated credentials**.
6. Add a new federated credential. For Google Cloud:
   * Select **Other issuer** for the federated credential scenario.
   * **Issuer**: `https://accounts.google.com`
   * **Subject identifier**: Enter the **Unique ID** of your Google Cloud Service Account (obtained in Section 1, Step 5).
   * **Audience**: `api://AzureADTokenExchange` (Should be the default).
   * Provide a name and description for the credential.

## 3. Configuring Azure SQL for Federated Identity

1. In the Azure Portal, go to your Azure SQL server.
2. Under **Settings** > **Microsoft Entra ID** (previously Active Directory admin), set an Entra ID admin for the server (if not already set).
3. In your database, create an external user corresponding to the **Azure AD App Registration** (not the GCP service account directly):

   ```sql
   CREATE USER [<azure-ad-app-registration-name>] FROM EXTERNAL PROVIDER;
   -- Or using Client ID:
   -- CREATE USER [<azure-ad-app-client-id>] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [<azure-ad-app-registration-name>];
   ALTER ROLE db_datawriter ADD MEMBER [<azure-ad-app-registration-name>];
   ```

   Replace `<azure-ad-app-registration-name>` with the display name of your Azure AD App Registration, or `<azure-ad-app-client-id>` with its Application (client) ID.

## 4. Configuring Cloud Run

1. Deploy your application to Cloud Run.
2. In the Cloud Run service settings (under the "Security" tab or similar when revising a deployment), ensure the **Service account** is set to the Google Cloud Service Account you configured in Section 1.
   * The application code (like the `Neolution.AzureSqlFederatedIdentity` library) will use this runtime service account identity via Application Default Credentials to request a Google ID token.

3. Configure your application's settings (e.g., via environment variables in Cloud Run) with:
   * Azure AD **Tenant ID**.
   * Azure AD **Application (client) ID** of the App Registration you set up in Section 2.
   * The Google Cloud **Service Account Email** (from Section 1, Step 4).
   * The connection string for your Azure SQL database.

The application will then:

* Request an ID token from Google for the configured service account, specifying the Azure AD application as the audience (e.g., `api://AzureADTokenExchange`).
* Use this Google ID token to request an Azure AD access token for Azure SQL, presenting the Google ID token as a federated credential.
* Use the Azure AD access token to connect to Azure SQL.

## 5. Azure Managed Identity Support

You can use Azure Managed Identities (system-assigned or user-assigned) instead of federated Google identities. Follow these steps:

1. In the Azure Portal, enable a Managed Identity on your compute resource (App Service, Virtual Machine, Function, etc.). For user-assigned identities, note the **Client ID**.
2. Ensure your Azure SQL server has an Entra ID admin configured under **Settings > Entra ID Admin**.
3. In your database, create an external user for the Managed Identity:

   ```sql
   -- System-assigned Managed Identity:
   CREATE USER [<resource-name>] FROM EXTERNAL PROVIDER;

   -- Or user-assigned Managed Identity (using client ID):
   CREATE USER [<user-assigned-client-id>] FROM EXTERNAL PROVIDER;

   ALTER ROLE db_datareader ADD MEMBER [<identity>];
   ALTER ROLE db_datawriter ADD MEMBER [<identity>];
   ```

4. Configure your application settings (e.g., in `appsettings.json`):

   ```json
   {
     "Neolution.AzureSqlFederatedIdentity": {
       "UseManagedIdentity": true,
       "ManagedIdentityClientId": "<optional-user-assigned-client-id>"
     }
   }
   ```

   Omitting `ManagedIdentityClientId` uses the system-assigned identity by default.

5. No code changes needed beyond registering services:

   ```csharp
   builder.Services.AddAzureSqlFederatedIdentity(builder.Configuration);
   ```

> Note: In **Managed Identity** mode, the library fetches tokens directly from Azure’s Instance Metadata Service (or the user-assigned identity) and does **not** use or require any Google service account.

## FAQ

### Does this package support Azure Managed Identities?

Yes. See Section 5 above for setup instructions.

---

## Extending to Other Identity Providers

Rather than duplicating nested sections, use a single top-level `Provider` setting with a matching section named for the chosen provider. Only that section needs to be populated:

### Managed Identity

```json
"Neolution.AzureSqlFederatedIdentity": {
  "Provider": "ManagedIdentity",

  "ManagedIdentity": {
    "ClientId": "<optional-user-assigned-client-id>"
  }
}
```

### Google Cloud

```json
"Neolution.AzureSqlFederatedIdentity": {
  "Provider": "Google",

  "Google": {
    "ClientId": "<azure-ad-app-client-id>",
    "TenantId": "<azure-entra-tenant-id>",
    "ServiceAccountEmail": "<gcp-service-account-email>"
  }
}
```

### AWS STS (Assume Role)

```json
"Neolution.AzureSqlFederatedIdentity": {
  "Provider": "Aws",

  "Aws": {
    "ClientId": "<azure-ad-app-client-id>",
    "TenantId": "<azure-entra-tenant-id>",
    "RoleArn": "arn:aws:iam::123456789012:role/MyFederatedRole",
    "SessionName": "MySqlSession"
  }
}
```

### GitHub OIDC

```json
"Neolution.AzureSqlFederatedIdentity": {
  "Provider": "GitHub",

  "GitHub": {
    "ClientId": "<azure-ad-app-client-id>",
    "TenantId": "<azure-entra-tenant-id>",
    "Repository": "org/repo",
    "Environment": "production"
  }
}
```

In your DI registration (`RegisterFederatedIdentityServices`), branch on the single `options.Provider` value to instantiate the matching `IAzureSqlTokenExchanger` implementation.  

---

For more details, see the official documentation for [Microsoft Entra ID](https://learn.microsoft.com/en-us/azure/active-directory/), [Azure SQL](https://learn.microsoft.com/en-us/azure/azure-sql/), [Google Cloud IAM](https://cloud.google.com/iam/docs/), and [Cloud Run](https://cloud.google.com/run/docs/).
