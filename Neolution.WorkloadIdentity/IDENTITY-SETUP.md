# Passwordless Connection Setup

This guide walks you through the steps in the Azure Portal and Google Cloud Console to configure passwordless (token-based) connections using the Neolution.WorkloadIdentity package.

## Choose your Identity Configuration

This library supports two primary identity configurations for accessing Azure resources:

- **Azure Managed Identity**: For workloads running within the Azure ecosystem (App Service, VMs, Functions, etc.). This is the simplest approach for Azure-hosted applications.
- **Workload Identity Federation**: For workloads running outside of Azure (e.g., on Google Cloud, GitHub Actions, etc.) that need to securely access Azure resources without secrets.

Follow the setup guide for your chosen configuration.

## Azure Managed Identity

Use this when your application is running on Azure.

### Enable or Create a Managed Identity

#### System-assigned

1. In the Azure Portal, navigate to your App Service, Function App, or VM.
2. Under **Settings** > **Identity**, switch **System-assigned** to **On**.
3. Click **Save**.
4. Under **Properties**, note the **Client ID** and **Tenant ID**.

#### User-assigned

1. In Azure Portal, search for **Managed Identities** and select **User-assigned**.
2. Click **+ Add**, give it a name and select a resource group.
3. Create the identity.
4. Navigate to your App Service/Function/VM and under **Identity**, click **+ Add User-assigned**, then select the identity.
5. Under **Managed Identity** > **Properties**, note the **Client ID** and **Tenant ID**.

### Grant Permissions to Azure Resources

#### Azure SQL

1. Connect to your Azure SQL database as an admin (e.g., using Azure Data Studio or SSMS).
2. Run the following T-SQL, replacing `[<identity-name>]` with the managed identity's name:

   ```sql
   CREATE USER [<identity-name>] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [<identity-name>];
   ALTER ROLE db_datawriter ADD MEMBER [<identity-name>];
   -- Optionally, grant other roles as needed
   ```

#### Azure Blob Storage

1. Go to your **Storage Account** resource.
2. Select **Access control (IAM)** > **Add** > **Add role assignment**.
3. Choose **Storage Blob Data Reader** (or **Storage Blob Data Contributor**) as the role.
4. Assign it to your managed identity.

### Update `appsettings.json` for Managed Identity

In your `.NET` application, configure the Azure sections under `Neolution.WorkloadIdentity`:

```json
{
  "Neolution.WorkloadIdentity": {
    "AzureSql": {
      "Provider": "ManagedIdentity",
      "ManagedIdentity": {
        "UseSystemAssignedIdentity": false,   // or true for system-assigned
        "ClientId": "00000000-0000-0000-0000-000000000000"
      }
    },
    "BlobStorage": {
      "Provider": "ManagedIdentity",
      "ManagedIdentity": {
        "ClientId": "00000000-0000-0000-0000-000000000000"
      }
    }
  }
}
```

Replace the `ClientId` with your managed identity's client ID, or omit it for system-assigned.

## Google Cloud to Azure (Workload Identity Federation)

Use this when your application is running on Google Cloud and needs to access Azure resources.

> **Pre-requisite**: Create a **User-assigned Managed Identity** in Azure (see above). This identity will be used as a service principal in Azure to bind your federated credentials.

### In Google Cloud: Create a Service Account

1. In the GCP Console, go to **IAM & Admin** > **Service Accounts**.
2. Click **Create Service Account**, enter a name (e.g., `azure-federation-sa`), and click **Create and Continue**.
3. Note the **Email** of the service account (e.g., `azure-federation-sa@<your-gcp-project>.iam.gserviceaccount.com`).
4. Click on the service account to go to its details page.
5. Find and copy the **Unique ID**. This is a 21-digit number and is required to set up the trust relationship in Azure AD.
6. (For local development) Go to the **Keys** tab, click **Add Key** > **Create new key**, select **JSON**, and download the key file. Set the `GOOGLE_APPLICATION_CREDENTIALS` environment variable to the path of this file.

### In Azure AD: Configure Federated Credential on User-assigned Managed Identity

1. In the Azure Portal’s top search bar, type the name of your **User-assigned Managed Identity** and select it from the search results.
2. On the managed identity’s overview page, click **Federated credentials** under the **Manage** section.
3. Click **Add credential**.
4. Fill in the fields:
   - **Issuer**: `https://accounts.google.com`
   - **Subject identifier**: The **Unique ID** of your Google Cloud Service Account (from the previous step).
   - **Audience**: `api://AzureADTokenExchange`
5. Click **Add** to save the federated credential.

### Grant Permissions to Azure Resources (Federation)

#### Azure SQL (federation)

1. Connect to your Azure SQL database as an admin.
2. Run the following T-SQL, replacing `<identity-name>` with the name of your User-assigned Managed Identity:

   ```sql
   CREATE USER [<identity-name>] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [<identity-name>];
   ALTER ROLE db_datawriter ADD MEMBER [<identity-name>];
   ```

#### Azure Blob Storage (federation)

1. Go to your **Storage Account** > **Access control (IAM)**.
2. Click **Add** > **Add role assignment**.
3. Select a role (e.g., **Storage Blob Data Contributor**).
4. For **Assign access to**, select **User, group, or service principal** and search for your User-assigned Managed Identity by name.
5. Select it and click **Save**.

### Update `appsettings.json` for Google Cloud Federation

Configure the `Google` provider section in your app settings:

```json
{
  "Neolution.WorkloadIdentity": {
    "AzureSql": {
      "Provider": "Google",
      "Google": {
        "TenantId": "<your-azure-ad-tenant-id>",
        "ClientId": "<your-azure-uami-client-id>",
        "ServiceAccountEmail": "<your-gcp-service-account-email>"
      }
    },
    "BlobStorage": {
      "Provider": "Google",
      "Google": {
        "TenantId": "<your-azure-ad-tenant-id>",
        "ClientId": "<your-azure-uami-client-id>",
        "ServiceAccountEmail": "<your-gcp-service-account-email>"
      }
    }
  }
}
```

Replace the placeholders with the values you noted in the previous steps.

## Security Best Practice: Disable SQL Authentication and Remove Password-based Users

After successfully enabling passwordless/federated identity for your Azure SQL Database, consider increasing your security posture by disabling SQL authentication and removing any users that authenticate with passwords. This reduces the attack surface and leverages the full benefits of passwordless access.

**Recommended Steps:**

1. **Audit Existing Users:**
   - Connect to your Azure SQL Database as an admin.
   - List all users:

     ```sql
     SELECT name, type_desc FROM sys.database_principals WHERE type IN ('S', 'U');
     ```

   - Identify users of type `SQL_USER` (these use passwords).

2. **Remove Password-based Users:**
   - For each password-based user you wish to remove, run:

     ```sql
     DROP USER [username];
     ```

   - Only do this after confirming that all applications and users have migrated to passwordless authentication.

3. **(Optional) Disable SQL Authentication at the Server Level:**
   - In the Azure Portal, navigate to your SQL Server resource.
   - Under **Settings** > **Active Directory admin**, ensure an Azure AD admin is set.
   - Under **Settings** > **SQL authentication**, set **Enforce Azure AD authentication** to **Enabled** (if available in your region/tier).
   - This will prevent new SQL logins from being created and block password-based connections.

**Why do this?**

- Eliminates the risk of password leaks or brute-force attacks.
- Enforces modern, token-based authentication for all access.
- Aligns with Microsoft and industry security best practices.

> **Note:** Always ensure you have at least one working Azure AD admin and that all applications are tested with passwordless authentication before removing password-based users or disabling SQL authentication.

For more details, see [Microsoft's official guidance](https://learn.microsoft.com/azure/azure-sql/database/authentication-aad-overview) on Azure AD authentication for SQL Database.
