# Quick Setup Guide: Teams Integration

## Prerequisites

- Microsoft 365 subscription with Teams
- Azure AD admin access (for app registration)
- Access to the Teams team/channel where notifications will be sent

## Step-by-Step Setup

### Step 1: Create or Configure Azure AD App

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** → **App registrations**
3. Either use existing app or click **New registration**
   - Name: `Intune Enrollment Notifier`
   - Supported account types: `Accounts in this organizational directory only`
   - Click **Register**

4. Note down:
   - **Application (client) ID**
   - **Directory (tenant) ID**

5. Create a client secret:
   - Go to **Certificates & secrets**
   - Click **New client secret**
   - Description: `Intune Notifier Secret`
   - Expires: Choose appropriate duration (recommend 12 months)
   - Click **Add**
   - **Copy the secret value immediately** (you won't see it again)

### Step 2: Configure API Permissions

1. In your app registration, go to **API permissions**
2. Click **Add a permission** → **Microsoft Graph** → **Application permissions**
3. Add these permissions:
   - `ChannelMessage.Send`
   - `Team.ReadBasic.All`
   - `Channel.ReadBasic.All`
4. Click **Add permissions**
5. Click **Grant admin consent for [Your Organization]**
6. Confirm by clicking **Yes**

### Step 3: Get Teams Team ID

**Method 1: Via Teams Web/Desktop App**
1. Open Microsoft Teams
2. Navigate to the team you want to use
3. Click the **...** (three dots) next to the team name
4. Select **Get link to team**
5. Copy the link - it will look like:
   ```
   https://teams.microsoft.com/l/team/19%3a...%40thread.tacv2/conversations?groupId=TEAM_ID_HERE&tenantId=...
   ```
6. Extract the `groupId` parameter - this is your **Team ID**

**Method 2: Via Graph Explorer**
1. Go to [Graph Explorer](https://developer.microsoft.com/en-us/graph/graph-explorer)
2. Sign in with your account
3. Run this query:
   ```
   GET https://graph.microsoft.com/v1.0/me/joinedTeams
   ```
4. Find your team in the response and copy the `id` field

### Step 4: Get Teams Channel ID

**Method 1: Via Teams Web/Desktop App**
1. Open Microsoft Teams
2. Navigate to the specific channel
3. Click the **...** (three dots) next to the channel name
4. Select **Get link to channel**
5. Copy the link - it will look like:
   ```
   https://teams.microsoft.com/l/channel/CHANNEL_ID_HERE/General?groupId=TEAM_ID&tenantId=...
   ```
6. Extract the channel ID from the URL (between `/channel/` and `/General`)

**Method 2: Via Graph Explorer**
1. Go to [Graph Explorer](https://developer.microsoft.com/en-us/graph/graph-explorer)
2. Run this query (replace `TEAM_ID` with your team ID):
   ```
   GET https://graph.microsoft.com/v1.0/teams/TEAM_ID/channels
   ```
3. Find your channel in the response and copy the `id` field

### Step 5: Update Azure Key Vault

1. Go to your Azure Key Vault in the Azure Portal
2. Navigate to **Secrets**
3. Add/update these secrets:

   **GRAPH-API-CLIENT-ID**
   - Value: Your Application (client) ID from Step 1

   **GRAPH-API-TENANT-ID**
   - Value: Your Directory (tenant) ID from Step 1

   **GRAPH-API-CLIENT-SECRET**
   - Value: Your client secret from Step 1

### Step 6: Update Azure Function Configuration

1. Go to your Azure Function App in the Azure Portal
2. Navigate to **Configuration** → **Application settings**
3. Add/update these settings:

   | Name | Value |
   |------|-------|
   | `NOTIFICATION_TYPE` | `Teams` (or `Both` for testing) |
   | `TEAMS_TEAM_ID` | Your Team ID from Step 3 |
   | `TEAMS_CHANNEL_ID` | Your Channel ID from Step 4 |
   | `KeyVaultUrl` | Your Key Vault URL (e.g., `https://your-keyvault.vault.azure.net/`) |

4. Click **Save**
5. Restart the Function App

### Step 7: Update Local Development Settings (Optional)

If testing locally, update `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "KeyVaultUrl": "https://your-keyvault.vault.azure.net/",
    "NOTIFICATION_TYPE": "Teams",
    "TEAMS_TEAM_ID": "your-team-id-here",
    "TEAMS_CHANNEL_ID": "your-channel-id-here",
    "GRAPH_API_CLIENT_ID": "your-client-id-here",
    "GRAPH_API_TENANT_ID": "your-tenant-id-here",
    "LOG_LEVEL": "Information"
  }
}
```

**Note:** For local development, you can put credentials in `local.settings.json`, but for production, always use Key Vault.

### Step 8: Test the Integration

1. **Health Check Test:**
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

2. **Send Test Notification:**
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
       "OsVersion": "11",
       "ProcessedDateTime": "2025-10-03T18:00:00Z"
     }'
   ```

3. Check your Teams channel for the notification

### Step 9: Monitor and Verify

1. Check Application Insights for any errors:
   - Go to Azure Portal → Your Function App → Application Insights
   - Look for exceptions or failed requests

2. Verify notifications are appearing in Teams

3. Monitor for a few days with `NOTIFICATION_TYPE` set to `Both` (if you want to compare)

## Troubleshooting

### "Forbidden" Error

**Cause:** App doesn't have proper permissions or admin consent not granted

**Solution:**
1. Verify API permissions are added
2. Ensure admin consent is granted (green checkmarks in Azure AD)
3. Wait 5-10 minutes for permissions to propagate

### "Team/Channel Not Found" Error

**Cause:** Incorrect Team ID or Channel ID

**Solution:**
1. Double-check IDs are copied correctly
2. Ensure no extra characters or spaces
3. Verify the app has access to the team

### "Unauthorized" Error

**Cause:** Client secret expired or incorrect

**Solution:**
1. Verify client secret in Key Vault is correct
2. Check secret hasn't expired
3. Create new secret if needed

### Notifications Not Appearing

**Cause:** Multiple possible issues

**Solution:**
1. Check Function App logs in Application Insights
2. Verify health check endpoint returns "Teams Connected"
3. Test Graph API permissions using Graph Explorer
4. Ensure bot/app is added to the team

## Configuration Options

### Notification Types

- **`Teams`**: Send only to Teams (recommended for cost savings)
- **`Email`**: Send only via SendGrid (legacy mode)
- **`Both`**: Send to both Teams and Email (useful for transition period)

### Switching Between Modes

Simply update the `NOTIFICATION_TYPE` setting in Azure Function configuration:

1. Go to Function App → Configuration
2. Update `NOTIFICATION_TYPE` value
3. Save and restart

No code changes required!

## Security Best Practices

1. **Never commit secrets to source control**
   - Use Key Vault for production
   - Use `local.settings.json` for local dev (gitignored)

2. **Rotate client secrets regularly**
   - Set expiration to 12 months
   - Create calendar reminder to rotate

3. **Use Managed Identity when possible**
   - Enable System-assigned identity on Function App
   - Grant Key Vault access to the identity

4. **Limit permissions**
   - Only grant necessary Graph API permissions
   - Use application permissions (not delegated)

## Cost Comparison

### Before (SendGrid)
- SendGrid: $0-$100/month
- Total: $13-$113/month

### After (Teams)
- Teams: $0 (included in M365)
- Total: $8/month

**Savings: $5-$105/month (38-90%)**

## Support

If you encounter issues:

1. Check Application Insights logs
2. Verify health check endpoint
3. Test Graph API permissions in Graph Explorer
4. Review Azure AD app configuration

## Additional Resources

- [Microsoft Graph API Documentation](https://docs.microsoft.com/en-us/graph/)
- [Teams Channel Messages API](https://docs.microsoft.com/en-us/graph/api/channel-post-messages)
- [Adaptive Cards Designer](https://adaptivecards.io/designer/)
- [Azure Functions Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/)

## Quick Reference

### Required Azure AD Permissions
- `ChannelMessage.Send` (Application)
- `Team.ReadBasic.All` (Application)
- `Channel.ReadBasic.All` (Application)

### Required Configuration Settings
- `NOTIFICATION_TYPE`: "Teams"
- `TEAMS_TEAM_ID`: Your team ID
- `TEAMS_CHANNEL_ID`: Your channel ID
- `KeyVaultUrl`: Your Key Vault URL

### Required Key Vault Secrets
- `GRAPH-API-CLIENT-ID`
- `GRAPH-API-TENANT-ID`
- `GRAPH-API-CLIENT-SECRET`

## Next Steps

After setup is complete:

1. ✅ Test with sample notification
2. ✅ Monitor for 24-48 hours
3. ✅ Switch from "Both" to "Teams" mode
4. ✅ Cancel SendGrid subscription
5. ✅ Document any customizations

Congratulations! Your Teams integration is now set up and ready to save costs while improving collaboration.
