# Intune Enrollment Notification System - Deployment Guide

## Prerequisites

### Azure Subscription Requirements
- Azure subscription with appropriate permissions
- Resource Group creation rights
- Azure AD Global Administrator or Application Administrator role
- Contributor role on the target subscription

### Required Tools
- Azure CLI (version 2.40+)
- Azure PowerShell (version 8.0+)
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
1. Navigate to Azure Portal → Azure Active Directory → App registrations
2. Select your app → API permissions
3. Add the following Microsoft Graph permissions:
   - `DeviceManagementManagedDevices.Read.All` (Application)
   - `DeviceManagementConfiguration.Read.All` (Application)
   - `User.Read.All` (Application)
4. Grant admin consent for all permissions

## Step 2: Deploy Azure Resources

### Using Azure CLI
```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "your-subscription-id"

# Create resource group
az group create --name "rg-intune-notify-prod" --location "East US"

# Deploy resources using Bicep
az deployment group create \
  --resource-group "rg-intune-notify-prod" \
  --template-file "azure-resources.bicep" \
  --parameters environment="prod" \
               sendGridApiKey="your-sendgrid-api-key" \
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

# Deploy resources
New-AzResourceGroupDeployment `
  -ResourceGroupName "rg-intune-notify-prod" `
  -TemplateFile "azure-resources.bicep" `
  -environment "prod" `
  -sendGridApiKey "your-sendgrid-api-key" `
  -notificationEmails @("admin@company.com", "it-team@company.com")
```

## Step 3: Configure SendGrid

### Create SendGrid Account
1. Sign up at [SendGrid](https://sendgrid.com/)
2. Verify your sender identity
3. Create an API key with Mail Send permissions
4. Configure domain authentication (recommended)

### Email Template Setup
Create a dynamic template in SendGrid with the following structure:
```html
<!DOCTYPE html>
<html>
<head>
    <title>Intune Enrollment Notification</title>
</head>
<body>
    <h2>{{event_type}} - Device Enrollment</h2>
    <table>
        <tr><td><strong>Device Name:</strong></td><td>{{device_name}}</td></tr>
        <tr><td><strong>User:</strong></td><td>{{user_name}}</td></tr>
        <tr><td><strong>Platform:</strong></td><td>{{platform}}</td></tr>
        <tr><td><strong>Status:</strong></td><td>{{status}}</td></tr>
        <tr><td><strong>Timestamp:</strong></td><td>{{timestamp}}</td></tr>
    </table>
    {{#if error_details}}
    <h3>Error Details</h3>
    <pre>{{error_details}}</pre>
    {{/if}}
    {{#if diagnostic_info}}
    <h3>Diagnostic Information</h3>
    <pre>{{diagnostic_info}}</pre>
    {{/if}}
</body>
</html>
```

## Step 4: Deploy Function App Code

### Function App Structure
```
IntuneNotificationFunction/
├── host.json
├── local.settings.json
├── IntuneNotificationFunction.csproj
├── ProcessEnrollmentEvent.cs
├── Models/
│   ├── EnrollmentEvent.cs
│   └── NotificationData.cs
└── Services/
    ├── GraphService.cs
    └── EmailService.cs
```

### Deploy Function Code
```bash
# Navigate to function app directory
cd IntuneNotificationFunction

# Build and publish
dotnet publish --configuration Release

# Deploy using Azure CLI
func azure functionapp publish intune-notify-prod-func --csharp
```

## Step 5: Configure Logic Apps Workflow

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

## Step 6: Configure Microsoft Graph Webhooks (Optional)

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

## Step 7: Testing and Validation

### Test Checklist
- [ ] Azure resources deployed successfully
- [ ] Function App is running and accessible
- [ ] Logic App workflow executes without errors
- [ ] Microsoft Graph API calls return data
- [ ] Email notifications are sent successfully
- [ ] Error handling works correctly
- [ ] Monitoring and logging are functional

### Test Scenarios
1. **Successful Enrollment**: Enroll a test device and verify notification
2. **Failed Enrollment**: Simulate enrollment failure and check error details
3. **Multiple Device Types**: Test with Windows, iOS, Android devices
4. **High Volume**: Test with multiple simultaneous enrollments
5. **Error Conditions**: Test API failures, email delivery issues

## Step 8: Monitoring and Maintenance

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

#### Email Delivery Problems
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
