# Passwordless Connection Setup

This guide walks you through the steps in the Azure Portal and Google Cloud Console to configure### Step 2: Configure Federated Credential in Azure

> **Why this step?** This creates the trust relationship between your Google Cloud service account and your Azure managed identity.

1. In the Azure Portal's top search bar, type the name of your **User-assigned Managed Identity** and select it from the search results.
2. On the managed identity's overview page, click **Federated credentials** under the **Manage** section.
3. Click **Add credential**.
4. Fill in the fields:
   - **Issuer**: `https://accounts.google.com`
   - **Subject identifier**: The **Unique ID** of your Google Cloud Service Account (from Step 1).
   - **Audience**: `api://AzureADTokenExchange`
5. Click **Add** to save the federated credential.

> ⚠️ **Important**: Use the service account's **Unique ID** (21-digit number), not its email address.ess (token-based) connections using the Neolution.WorkloadIdentity package.

## Key Concepts

Before you begin, here are the key terms used in this guide:

- **Managed Identity (UAMI)**: An Azure identity for your application that can access Azure resources without storing credentials in code.
- **Federated Credential**: A trust rule in Azure that allows an external identity (like a Google Cloud service account) to act as your Azure managed identity.
- **Service Account**: A Google Cloud identity that your application uses to authenticate.
- **Workload Identity Federation**: A secure way to let workloads running outside Azure access Azure resources without secrets.

## Choose your Identity Configuration

This library supports two primary identity configurations for accessing Azure resources:

- **Azure Managed Identity**: For workloads running within the Azure ecosystem (App Service, VMs, Functions, etc.). This is the simplest approach for Azure-hosted applications.
- **Workload Identity Federation**: For workloads running outside of Azure (e.g., on Google Cloud, GitHub Actions, etc.) that need to securely access Azure resources without secrets.

### Which Path Should I Use?

Use this simple checklist to decide:

- ✅ **My app runs on Azure** (App Service, VM, Function, etc.) → Use **Azure Managed Identity**
- ✅ **My app runs on Google Cloud** (Cloud Run, GKE, Compute Engine) → Use **Workload Identity Federation**
- ✅ **My app runs on GitHub Actions, AWS, or other platforms** → Use **Workload Identity Federation**

Follow the setup guide for your chosen configuration.

## Azure Managed Identity

Use this when your application is running on Azure.

### Enable or Create a Managed Identity

> **Why this step?** A managed identity acts as your application's identity in Azure, allowing it to access Azure resources without storing credentials in your code.

#### System-assigned

1. In the Azure Portal, navigate to your App Service, Function App, or VM.
2. Under **Settings** > **Identity**, switch **System-assigned** to **On**.
3. Click **Save**.
4. Under **Properties**, note the **Client ID** and **Tenant ID**.

> 📋 **Copy these values**: You'll need the Client ID for your `appsettings.json` configuration.

#### User-assigned

1. In Azure Portal, search for **Managed Identities** and select **User-assigned**.
2. Click **+ Add**, give it a name and select a resource group.
3. Create the identity.
4. Navigate to your App Service/Function/VM and under **Identity**, click **+ Add User-assigned**, then select the identity.
5. Under **Managed Identity** > **Properties**, note the **Client ID** and **Tenant ID**.

> 📋 **Copy these values**: You'll need the Client ID for your `appsettings.json` configuration.

### Grant Permissions to Azure Resources

> **Why this step?** Your managed identity needs explicit permissions to access Azure SQL databases and Blob Storage accounts.

#### Azure SQL

1. Connect to your Azure SQL database as an admin (e.g., using Azure Data Studio or SSMS).
2. Run the following T-SQL, replacing `[<identity-name>]` with the managed identity's name:

   ```sql
   CREATE USER [<identity-name>] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [<identity-name>];
   ALTER ROLE db_datawriter ADD MEMBER [<identity-name>];
   -- Optionally, grant other roles as needed
   ```

> 💡 **Tip**: The identity name is the display name you gave your managed identity, not the Client ID.

#### Azure Blob Storage

1. Go to your **Storage Account** resource.
2. Select **Access control (IAM)** > **Add** > **Add role assignment**.
3. Choose **Storage Blob Data Reader** (or **Storage Blob Data Contributor**) as the role.
4. Assign it to your managed identity.

### Update `appsettings.json` for Managed Identity

Configure your `.NET` application with the managed identity settings:

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

**Where to find these values:**

- `ClientId`: Copy this from the managed identity's **Properties** page in the Azure Portal
- For system-assigned identities: Set `UseSystemAssignedIdentity` to `true` and omit the `ClientId`

## Google Cloud to Azure (Workload Identity Federation)

Use this when your application is running on Google Cloud and needs to access Azure resources.

> **How this works**: Your Google Cloud service account will be trusted by an Azure managed identity, allowing your GCP app to get Azure access tokens without storing any secrets.

> **Pre-requisite**: Create a **User-assigned Managed Identity** in Azure (see above). This identity will be used as a service principal in Azure to bind your federated credentials.

### Step 1: Create a Google Cloud Service Account

> **Why this step?** This creates the identity that your GCP application will use to authenticate and request Azure tokens.

1. In the GCP Console, go to **IAM & Admin** > **Service Accounts**.
2. Click **Create Service Account**, enter a name (e.g., `azure-federation-sa`), and click **Create and Continue**.
3. Note the **Email** of the service account (e.g., `azure-federation-sa@<your-gcp-project>.iam.gserviceaccount.com`).
4. Click on the service account to go to its details page.
5. Find and copy the **Unique ID**. This is a 21-digit number and is required to set up the trust relationship in Azure AD.
6. (For local development) Go to the **Keys** tab, click **Add Key** > **Create new key**, select **JSON**, and download the key file. Set the `GOOGLE_APPLICATION_CREDENTIALS` environment variable to the path of this file.

> 📋 **Copy these values**: You'll need the **Service Account Email** and **Unique ID** for the next steps.

### In Azure AD: Configure Federated Credential on User-assigned Managed Identity

1. In the Azure Portal’s top search bar, type the name of your **User-assigned Managed Identity** and select it from the search results.
2. On the managed identity’s overview page, click **Federated credentials** under the **Manage** section.
3. Click **Add credential**.
4. Fill in the fields:
   - **Issuer**: `https://accounts.google.com`
   - **Subject identifier**: The **Unique ID** of your Google Cloud Service Account (from the previous step).
   - **Audience**: `api://AzureADTokenExchange`
5. Click **Add** to save the federated credential.

### Step 3: Grant Azure Resource Permissions

> **Why this step?** Your User-assigned Managed Identity needs permissions to access your Azure SQL databases and Blob Storage accounts.

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

### Step 4: Configure Your Application

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

**Where to find these values:**

- `TenantId`: Your Azure AD tenant ID (found in Azure Portal > Azure Active Directory > Overview)
- `ClientId`: The Client ID of your User-assigned Managed Identity (from its Properties page)
- `ServiceAccountEmail`: The email address of your Google Cloud service account (from Step 1)

## Troubleshooting & Common Issues

### Common Mistakes

- ❌ **Using the service account email instead of Unique ID**: In the federated credential, make sure you use the 21-digit Unique ID, not the email.
- ❌ **Wrong Client ID**: Use the User-assigned Managed Identity's Client ID, not a system-assigned one.
- ❌ **Missing permissions**: Ensure your managed identity has permissions on both Azure SQL and Blob Storage.
- ❌ **Wrong audience**: The audience must be exactly `api://AzureADTokenExchange`.

### Testing Your Setup

To verify your setup is working, you can check that your application can acquire tokens. Look for successful authentication in your application logs when accessing Azure resources.

### Helpful Links

- [Azure Managed Identities Documentation](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/)
- [Google Cloud Service Accounts](https://cloud.google.com/iam/docs/service-accounts)
- [Azure AD Workload Identity Federation](https://docs.microsoft.com/en-us/azure/active-directory/develop/workload-identity-federation)
