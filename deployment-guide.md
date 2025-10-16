# Intune Enrollment Notification System - Deployment Guide

## Deployment Options

This guide provides two deployment approaches:
1. **Automated PowerShell Script (Recommended)** - Single-command deployment (15 minutes)
2. **Manual Step-by-Step** - Detailed manual deployment process (2 hours)

---

## Option 1: Automated PowerShell Deployment (Recommended)

### Prerequisites for Automated Deployment

#### Azure Subscription Requirements
- Azure subscription with appropriate permissions
- Resource Group creation rights
- Azure AD Global Administrator or Application Administrator role
- Contributor role on the target subscription

#### Required Tools
- **PowerShell 7.0+** - [Download](https://github.com/PowerShell/PowerShell/releases)
- **Azure PowerShell (Az module 8.0+)**
  ```powershell
  Install-Module -Name Az -Repository PSGallery -Force
  ```
- **AzureAD PowerShell module**
  ```powershell
  Install-Module -Name AzureAD -Repository PSGallery -Force
  ```
- **.NET 6.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/6.0)
- **Azure Functions Core Tools v4** - [Download](https://docs.microsoft.com/azure/azure-functions/functions-run-local)

### Quick Start with PowerShell Script

#### Step 1: Prepare Configuration File

Create or edit `deployment-config.json`:

```json
{
  "environment": "prod",
  "location": "Canada Central",
  "resourceGroupName": "rg-intune-notify-prod",
  "notificationType": "Teams",
  "teamsTeamId": "your-teams-team-id",
  "teamsChannelId": "your-teams-channel-id",
  "sendGridApiKey": "",
  "notificationEmails": "admin@company.com"
}
```

**Get Teams IDs:**
1. Open Microsoft Teams
2. Navigate to your team → Click "..." → "Get link to team"
3. Extract the `groupId` from the URL (this is your Team ID)
4. Navigate to your channel → Click "..." → "Get link to channel"
5. Extract the channel ID from the URL

See [SETUP_TEAMS.md](SETUP_TEAMS.md) for detailed instructions.

#### Step 2: Run Deployment Script

```powershell
# Deploy using configuration file (recommended)
.\Deploy-IntuneNotifier.ps1 -ConfigFile .\deployment-config.json

# Or deploy with inline parameters
.\Deploy-IntuneNotifier.ps1 `
  -Environment prod `
  -Location "Canada Central" `
  -NotificationType Teams `
  -TeamsTeamId "your-team-id" `
  -TeamsChannelId "your-channel-id"
```

#### Step 3: Grant API Permissions

After the script creates the Azure AD app registration:
1. Navigate to Azure Portal → Azure Active Directory → App registrations
2. Find "Intune Enrollment Notifier - {environment}"
3. Click API permissions → Add required Microsoft Graph permissions:
   - `DeviceManagementManagedDevices.Read.All` (Application)
   - `DeviceManagementConfiguration.Read.All` (Application)
   - `User.Read.All` (Application)
   - `ChannelMessage.Send` (Application)
   - `Team.ReadBasic.All` (Application)
   - `Channel.ReadBasic.All` (Application)
4. Click "Grant admin consent"

#### Step 4: Configure Key Vault Secrets

The script outputs the Key Vault name. Add the Graph API credentials:

```powershell
$keyVaultName = "your-keyvault-name"  # From script output

# Add Graph API credentials (from app registration)
az keyvault secret set --vault-name $keyVaultName --name "GRAPH-API-CLIENT-ID" --value "your-client-id"
az keyvault secret set --vault-name $keyVaultName --name "GRAPH-API-TENANT-ID" --value "your-tenant-id"
az keyvault secret set --vault-name $keyVaultName --name "GRAPH-API-CLIENT-SECRET" --value "your-client-secret"
```

#### Step 5: Verify Deployment

Test the health endpoint:
```powershell
$functionUrl = "your-function-url"  # From script output
Invoke-RestMethod -Uri "$functionUrl/api/health" -Method Get
```

Expected response:
```json
{
  "Status": "Healthy",
  "Services": {
    "GraphService": "Connected",
    "NotificationService": "Teams Connected"
  }
}
```

### Deployment Log

All deployment activities are logged to:
```
%USERPROFILE%\Documents\Deploy-IntuneNotifier-YYYY-MM-DD-HH-MM.log
```

Review this log for troubleshooting or audit purposes.

### Script Parameters

| Parameter | Description | Default | Required |
|-----------|-------------|---------|----------|
| `Environment` | Target environment (dev/staging/prod) | prod | No |
| `Location` | Azure region | Canada Central | No |
| `ResourceGroupName` | Resource group name | rg-intune-notify-{env} | No |
| `NotificationType` | Teams, Email, or Both | Teams | No |
| `TeamsTeamId` | Microsoft Teams Team ID | - | If Teams |
| `TeamsChannelId` | Microsoft Teams Channel ID | - | If Teams |
| `SendGridApiKey` | SendGrid API key | - | If Email |
| `NotificationEmails` | Comma-separated email list | - | If Email |
| `ConfigFile` | Path to JSON config file | - | No |
| `SkipAppRegistration` | Skip app registration step | false | No |
| `SkipResourceDeployment` | Skip resource deployment | false | No |

### Troubleshooting Automated Deployment

**Prerequisites check fails:**
- Ensure all required tools are installed and in PATH
- Verify PowerShell version: `$PSVersionTable.PSVersion`

**Azure connection fails:**
- Run `Connect-AzAccount` manually
- Verify you have appropriate permissions

**Bicep deployment fails:**
- Check resource naming conflicts
- Verify subscription quota limits
- Review deployment log for specific errors

**Function deployment fails:**
- Ensure .NET SDK is installed: `dotnet --version`
- Verify Azure Functions Core Tools: `func --version`
- Check Function App is running in Azure Portal

---

## Option 2: Manual Step-by-Step Deployment

### Prerequisites

#### Azure Subscription Requirements
- Azure subscription with appropriate permissions
- Resource Group creation rights
- Azure AD Global Administrator or Application Administrator role
- Contributor role on the target subscription

#### Required Tools
- Azure CLI (version 2.40+)
    https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?view=azure-cli-latest&pivots=msi
- Azure PowerShell (version 8.0+)
    Install-Module -Name Az -Repository PSGallery -Force
- Visual Studio Code with Azure extensions
- .NET 6 SDK
- Git

## Step 1: Azure AD App Registration

### Create App Registration
```powershell
# Connect to Azure AD
Connect-AzureAD

# Create the app registration
$app = New-AzureADApplication -DisplayName "Intune Enrollment Notifier" -ReplyUrls @("https://localhost")

# Create service principal
$sp = New-AzureADServicePrincipal -AppId $app.AppId

# Generate client secret (save this securely)
$secret = New-AzureADApplicationPasswordCredential -ObjectId $app.ObjectId -CustomKeyIdentifier "IntuneNotifierSecret"

Write-Host "Application ID: $($app.AppId)"
Write-Host "Tenant ID: $((Get-AzureADTenantDetail).ObjectId)"
Write-Host "Client Secret: $($secret.Value)"
```

### Configure API Permissions
1. Navigate to Azure Portal → Azure Active Directory → App registrations → Intune Enrollment Notifier
2. Select your app → API permissions
3. Add the following Microsoft Graph permissions:
   - `DeviceManagementManagedDevices.Read.All` (Application)
   - `DeviceManagementConfiguration.Read.All` (Application)
   - `User.Read.All` (Application)
   - `ChannelMessage.Send` (Application) - **Required for Teams notifications**
   - `Team.ReadBasic.All` (Application) - **Required for Teams notifications**
   - `Channel.ReadBasic.All` (Application) - **Required for Teams notifications**
4. Grant admin consent for all permissions

## Step 2: Configure Notification Method

### Option A: Microsoft Teams (Recommended - Cost Optimized)

#### Get Teams Team and Channel IDs
1. Open Microsoft Teams
2. Navigate to your team → Click "..." → "Get link to team"
3. Extract the `groupId` from the URL (this is your Team ID)
4. Navigate to your channel → Click "..." → "Get link to channel"
5. Extract the channel ID from the URL

**See [SETUP_TEAMS.md](SETUP_TEAMS.md) for detailed instructions.**

### Option B: SendGrid Email (Legacy)

#### Create SendGrid Account
1. Sign up at [SendGrid](https://sendgrid.com/)
2. Verify your sender identity
3. Create an API key with Mail Send permissions
4. Configure domain authentication (recommended)

## Step 3: Deploy Azure Resources

### Using Azure CLI
```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "your-subscription-id"

# Create resource group
az group create --name "rg-intunenotify-canada-central" --location "Canada Central"

# Deploy resources using Bicep (Teams configuration)
az deployment group create \
  --resource-group "rg-intune-notify-prod" \
  --template-file "azure-resources.bicep" \
  --parameters environment="prod" \
               notificationType="Teams" \
               teamsTeamId="your-team-id" \
               teamsChannelId="your-channel-id" \
               notificationEmails='["admin@company.com","it-team@company.com"]'
```

### Using Azure PowerShell
```powershell
# Connect to Azure
Connect-AzAccount

# Set subscription context
Set-AzContext -SubscriptionId "your-subscription-id"

# Create resource group
New-AzResourceGroup -Name "rg-intune-notify-prod" -Location "East US"

# Deploy resources (Teams configuration)
New-AzResourceGroupDeployment `
  -ResourceGroupName "rg-intune-notify-prod" `
  -TemplateFile "azure-resources.bicep" `
  -environment "prod" `
  -notificationType "Teams" `
  -teamsTeamId "your-team-id" `
  -teamsChannelId "your-channel-id" `
  -notificationEmails @("admin@company.com", "it-team@company.com")
```

## Step 4: Configure Azure Key Vault Secrets

### Store Required Secrets
```bash
# Store Graph API credentials
az keyvault secret set --vault-name "your-keyvault" --name "GRAPH-API-CLIENT-ID" --value "your-client-id"
az keyvault secret set --vault-name "your-keyvault" --name "GRAPH-API-TENANT-ID" --value "your-tenant-id"
az keyvault secret set --vault-name "your-keyvault" --name "GRAPH-API-CLIENT-SECRET" --value "your-client-secret"

# Store SendGrid API key (if using email)
az keyvault secret set --vault-name "your-keyvault" --name "SendGridApiKey" --value "your-sendgrid-key"
```

### Grant Function App Access to Key Vault
```bash
# Enable system-assigned managed identity
az functionapp identity assign --name "your-function-app" --resource-group "rg-intune-notify-prod"

# Grant access to Key Vault
az keyvault set-policy --name "your-keyvault" \
  --object-id "<function-app-managed-identity-id>" \
  --secret-permissions get list
```

## Step 5: Deploy Function App Code

### Function App Structure
```
IntuneNotificationFunction/
├── host.json
├── local.settings.json
├── IntuneNotificationFunction.csproj
├── ProcessEnrollmentEvent.cs
├── Startup.cs
├── Models/
│   ├── EnrollmentEvent.cs
│   └── NotificationData.cs
└── Services/
    ├── GraphService.cs
    ├── EmailService.cs
    └── TeamsNotificationService.cs  # NEW: Teams integration
```

### Configure Function App Settings
```bash
# Set notification type (Teams, Email, or Both)
az functionapp config appsettings set \
  --name "your-function-app" \
  --resource-group "rg-intune-notify-prod" \
  --settings NOTIFICATION_TYPE="Teams"

# Set Teams configuration
az functionapp config appsettings set \
  --name "your-function-app" \
  --resource-group "rg-intune-notify-prod" \
  --settings TEAMS_TEAM_ID="your-team-id" TEAMS_CHANNEL_ID="your-channel-id"

# Set Key Vault URL
az functionapp config appsettings set \
  --name "your-function-app" \
  --resource-group "rg-intune-notify-prod" \
  --settings KeyVaultUrl="https://your-keyvault.vault.azure.net/"
```

### Deploy Function Code
```bash
# Navigate to function app directory
cd src/IntuneNotificationFunction

# Build and publish
dotnet publish --configuration Release

# Deploy using Azure Functions Core Tools
func azure functionapp publish intune-notify-prod-func --csharp
```

## Step 6: Configure Logic Apps Workflow (Optional)

### Create Workflow Definition
1. Navigate to Logic Apps in Azure Portal
2. Create new workflow named "IntuneEnrollmentMonitor"
3. Use the following workflow definition:

```json
{
    "definition": {
        "$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
        "actions": {
            "Get_Intune_Devices": {
                "inputs": {
                    "authentication": {
                        "type": "ManagedServiceIdentity"
                    },
                    "method": "GET",
                    "uri": "https://graph.microsoft.com/v1.0/deviceManagement/managedDevices"
                },
                "runAfter": {},
                "type": "Http"
            },
            "For_each_device": {
                "actions": {
                    "Call_Function": {
                        "inputs": {
                            "body": "@item()",
                            "function": {
                                "id": "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Web/sites/{function-app}/functions/ProcessEnrollmentEvent"
                            }
                        },
                        "runAfter": {},
                        "type": "Function"
                    }
                },
                "foreach": "@body('Get_Intune_Devices')?['value']",
                "runAfter": {
                    "Get_Intune_Devices": [
                        "Succeeded"
                    ]
                },
                "type": "Foreach"
            }
        },
        "triggers": {
            "Recurrence": {
                "recurrence": {
                    "frequency": "Minute",
                    "interval": 5
                },
                "type": "Recurrence"
            }
        }
    }
}
```

## Step 7: Configure Microsoft Graph Webhooks (Optional)

### Create Webhook Subscription
```csharp
// C# code to create webhook subscription
var subscription = new Subscription
{
    ChangeType = "created,updated",
    NotificationUrl = "https://your-logic-app-url/webhook",
    Resource = "deviceManagement/managedDevices",
    ExpirationDateTime = DateTimeOffset.UtcNow.AddDays(3),
    ClientState = "secretClientValue"
};

await graphServiceClient.Subscriptions
    .Request()
    .AddAsync(subscription);
```

## Step 8: Testing and Validation

### Test Checklist
- [ ] Azure resources deployed successfully
- [ ] Function App is running and accessible
- [ ] Azure AD app permissions granted
- [ ] Key Vault secrets configured
- [ ] Microsoft Graph API calls return data
- [ ] Teams/Email notifications are sent successfully
- [ ] Health check endpoint returns "Healthy"
- [ ] Error handling works correctly
- [ ] Monitoring and logging are functional

### Health Check Test
```bash
curl https://your-function-app.azurewebsites.net/api/health
```

Expected response:
```json
{
  "Status": "Healthy",
  "Services": {
    "GraphService": "Connected",
    "NotificationService": "Teams Connected"
  }
}
```

### Test Scenarios
1. **Successful Enrollment**: Enroll a test device and verify notification
2. **Failed Enrollment**: Simulate enrollment failure and check error details
3. **Multiple Device Types**: Test with Windows, iOS, Android devices
4. **High Volume**: Test with multiple simultaneous enrollments
5. **Error Conditions**: Test API failures, notification delivery issues
6. **Teams Notification**: Verify Adaptive Card appears in Teams channel
7. **Notification Switching**: Test switching between Teams/Email/Both modes

### Manual Test Notification
```bash
curl -X POST https://your-function-app.azurewebsites.net/api/ProcessEnrollmentEvent \
  -H "Content-Type: application/json" \
  -H "x-functions-key: YOUR_FUNCTION_KEY" \
  -d '{
    "DeviceName": "TEST-DEVICE",
    "EventType": "Success",
    "UserDisplayName": "Test User",
    "UserPrincipalName": "test@company.com",
    "OperatingSystem": "Windows",
    "OsVersion": "11"
  }'
```

## Step 9: Monitoring and Maintenance

### Application Insights Configuration
1. Navigate to Application Insights in Azure Portal
2. Configure alerts for:
   - Function execution failures
   - High response times
   - Email delivery failures
   - API quota exceeded

### Log Analytics Queries
```kusto
// Failed function executions
traces
| where severityLevel >= 3
| where timestamp > ago(1h)
| project timestamp, message, severityLevel

// Email delivery status
customEvents
| where name == "EmailSent"
| summarize count() by tostring(customDimensions.Status)
```

### Maintenance Tasks
- **Weekly**: Review error logs and performance metrics
- **Monthly**: Update dependencies and security patches
- **Quarterly**: Review and optimize costs
- **Annually**: Security audit and compliance review

## Troubleshooting

### Common Issues

#### Authentication Errors
- Verify Azure AD app permissions
- Check service principal credentials
- Ensure admin consent is granted

#### Function App Issues
- Check application settings
- Verify Key Vault access
- Review function logs in Application Insights

#### Notification Delivery Problems

**Teams Notifications:**
- Verify Graph API permissions are granted
- Check Team/Channel IDs are correct
- Ensure client secret hasn't expired
- Verify app has access to the team

**Email Notifications:**
- Validate SendGrid API key
- Check sender authentication
- Review email template syntax

#### Logic App Failures
- Verify managed identity permissions
- Check HTTP connector configurations
- Review workflow run history

### Support Resources
- [Microsoft Graph API Documentation](https://docs.microsoft.com/graph/)
- [Azure Logic Apps Documentation](https://docs.microsoft.com/azure/logic-apps/)
- [Azure Functions Documentation](https://docs.microsoft.com/azure/azure-functions/)
- [Teams Integration Guide](TEAMS_INTEGRATION.md)
- [Cost Optimization Guide](COST_OPTIMIZATION.md)
- [Setup Teams Guide](SETUP_TEAMS.md)
- [SendGrid API Documentation](https://docs.sendgrid.com/api-reference)

## Security Considerations

### Best Practices
- Use managed identities where possible
- Store secrets in Key Vault
- Enable diagnostic logging
- Implement least privilege access
- Regular security updates
- Monitor for suspicious activity

### Compliance
- Ensure GDPR compliance for personal data
- Implement data retention policies
- Document data processing activities
- Regular compliance audits

## Cost Optimization

### Teams vs Email Cost Comparison

**Teams Integration (Recommended):**
- Teams: $0 (included in M365 licenses)
- Azure Function: ~$2/month (optimized)
- Storage: ~$2/month
- Application Insights: ~$3/month
- Key Vault: ~$1/month
- **Total: ~$8/month**

**Email Integration (Legacy):**
- SendGrid: $0-$100/month (varies by volume)
- Azure Function: ~$5/month
- Storage: ~$2/month
- Application Insights: ~$5/month
- Key Vault: ~$1/month
- **Total: ~$13-$113/month**

**Savings with Teams: 76-90% cost reduction**

See [COST_OPTIMIZATION.md](COST_OPTIMIZATION.md) for detailed analysis.

## Notification Type Configuration

### Switching Between Notification Methods

You can change the notification method without redeploying code:

```bash
# Switch to Teams only
az functionapp config appsettings set \
  --name "your-function-app" \
  --resource-group "rg-intune-notify-prod" \
  --settings NOTIFICATION_TYPE="Teams"

# Switch to Email only
az functionapp config appsettings set \
  --name "your-function-app" \
  --resource-group "rg-intune-notify-prod" \
  --settings NOTIFICATION_TYPE="Email"

# Use both (for testing/transition)
az functionapp config appsettings set \
  --name "your-function-app" \
  --resource-group "rg-intune-notify-prod" \
  --settings NOTIFICATION_TYPE="Both"
```

### Recommended Deployment Strategy

1. **Phase 1 (Week 1):** Deploy with `NOTIFICATION_TYPE="Both"`
2. **Phase 2 (Week 2):** Monitor both channels, verify Teams reliability
3. **Phase 3 (Week 3):** Switch to `NOTIFICATION_TYPE="Teams"`
4. **Phase 4 (Week 4):** Cancel SendGrid subscription (optional)
