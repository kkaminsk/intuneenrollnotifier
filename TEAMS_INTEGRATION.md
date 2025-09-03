# Teams Integration & Cost Optimization Guide

## Overview

This document explains the Teams integration implementation and Azure cost optimization strategies for the Intune Enrollment Notifier application.

## Cost Optimization Summary

### Before Optimization
- **SendGrid**: Pay-per-email pricing (varies by volume)
- **Azure Function**: 5-minute timeout (potentially excessive)
- **Storage**: Development storage in production settings
- **Secrets**: Hardcoded in configuration files (security risk)

### After Optimization
- **Teams Integration**: Free with existing Microsoft 365 licenses
- **Azure Function**: Configurable timeout (recommend 1-2 minutes)
- **Storage**: Production Azure Storage account recommended
- **Secrets**: Stored in Azure Key Vault (already configured)

### Estimated Cost Savings
- **SendGrid Elimination**: $0-$100+/month depending on email volume
- **Function Compute**: 60-80% reduction with shorter timeout
- **Total Estimated Savings**: $50-$150/month for typical usage

## Teams Integration

### Benefits
1. **Cost Reduction**: No per-message fees (uses existing M365 licenses)
2. **Better Collaboration**: Notifications in team channels for immediate visibility
3. **Rich Formatting**: Adaptive Cards with interactive elements
4. **Centralized Communication**: All notifications in one place
5. **No External Dependencies**: No third-party email service required

### Implementation Details

The solution now supports three notification modes:
- **Teams**: Send notifications to Microsoft Teams channel only
- **Email**: Send notifications via SendGrid (legacy mode)
- **Both**: Send to both Teams and Email

### Configuration

#### 1. Azure AD App Registration

You need to configure an Azure AD application with the following permissions:

**Required API Permissions:**
- `Microsoft Graph` → `ChannelMessage.Send` (Application)
- `Microsoft Graph` → `Team.ReadBasic.All` (Application)
- `Microsoft Graph` → `Channel.ReadBasic.All` (Application)

**Steps:**
1. Go to Azure Portal → Azure Active Directory → App Registrations
2. Create new registration or use existing one
3. Go to "API Permissions" → "Add a permission" → "Microsoft Graph"
4. Select "Application permissions"
5. Add the permissions listed above
6. Click "Grant admin consent"

#### 2. Get Teams Channel IDs

**Get Team ID:**
1. Open Microsoft Teams
2. Click "..." next to your team name
3. Select "Get link to team"
4. The URL contains the team ID: `groupId=TEAM_ID_HERE`

**Get Channel ID:**
1. Open the channel in Teams
2. Click "..." next to channel name
3. Select "Get link to channel"
4. The URL contains: `groupId=TEAM_ID&threadId=CHANNEL_ID`

#### 3. Update Configuration

**local.settings.json** (for local development):
```json
{
  "Values": {
    "NOTIFICATION_TYPE": "Teams",
    "TEAMS_TEAM_ID": "your-team-id-here",
    "TEAMS_CHANNEL_ID": "your-channel-id-here",
    "GRAPH_API_CLIENT_ID": "your-app-client-id",
    "GRAPH_API_TENANT_ID": "your-tenant-id"
  }
}
```

**Azure Key Vault** (for production):
Store these secrets in Key Vault:
- `GRAPH-API-CLIENT-ID`: Your Azure AD app client ID
- `GRAPH-API-TENANT-ID`: Your Azure AD tenant ID
- `GRAPH-API-CLIENT-SECRET`: Your Azure AD app client secret

**Azure Function App Settings**:
- `NOTIFICATION_TYPE`: "Teams", "Email", or "Both"
- `TEAMS_TEAM_ID`: Your Teams team ID
- `TEAMS_CHANNEL_ID`: Your Teams channel ID

### Notification Format

Teams notifications use Adaptive Cards with:
- Status icon (✅ Success, ❌ Failure, ⚠️ Warning)
- Device information (name, user, platform)
- Diagnostic information
- Troubleshooting steps
- Color-coded status indicators

## Azure Resource Optimization

### 1. Function Timeout Optimization

**Current Setting**: 5 minutes
**Recommended**: 1-2 minutes

Update `host.json`:
```json
{
  "functionTimeout": "00:01:00"
}
```

**Rationale**: Most enrollment events process in seconds. Shorter timeout reduces compute costs.

### 2. Application Insights Sampling

**Current Setting**: Enabled with Request exclusion
**Optimization**: Adjust sampling rate if needed

Update `host.json`:
```json
{
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 5,
        "excludedTypes": "Request"
      }
    }
  }
}
```

### 3. Storage Account

**Current**: Development storage
**Recommended**: Azure Storage (Standard tier)

- Use Standard LRS (Locally Redundant Storage) for cost efficiency
- Enable lifecycle management to archive old logs
- Estimated cost: $1-5/month

### 4. Function App Hosting Plan

**Recommended**: Consumption Plan (pay-per-execution)
- First 1 million executions free
- $0.20 per million executions after
- Ideal for event-driven workloads

**Alternative**: Premium Plan (if needed)
- Only if you need VNet integration or longer execution times
- More expensive but provides better performance

### 5. Key Vault Optimization

**Current**: Configured but not fully utilized
**Recommendation**: Store all secrets in Key Vault

Move these to Key Vault:
- SendGrid API key (if still using email)
- Graph API credentials
- Any other sensitive configuration

**Cost**: $0.03 per 10,000 operations (minimal)

## Migration Path

### Phase 1: Testing (Recommended)
1. Set `NOTIFICATION_TYPE` to "Both"
2. Configure Teams channel
3. Monitor both email and Teams notifications
4. Verify Teams notifications work correctly

### Phase 2: Transition
1. Set `NOTIFICATION_TYPE` to "Teams"
2. Disable SendGrid (keep configuration for rollback)
3. Monitor for issues

### Phase 3: Cleanup
1. Remove SendGrid package reference (optional)
2. Remove SendGrid configuration
3. Cancel SendGrid subscription

## Rollback Plan

If Teams integration has issues:
1. Set `NOTIFICATION_TYPE` back to "Email"
2. Verify SendGrid credentials are still valid
3. Function will immediately revert to email notifications

## Testing

### Health Check Endpoint
```
GET https://your-function-app.azurewebsites.net/api/health
```

Response includes notification service status:
```json
{
  "Status": "Healthy",
  "Services": {
    "GraphService": "Connected",
    "NotificationService": "Teams Connected"
  }
}
```

### Manual Test
Send a test enrollment event:
```bash
curl -X POST https://your-function-app.azurewebsites.net/api/ProcessEnrollmentEvent \
  -H "Content-Type: application/json" \
  -d '{
    "DeviceName": "TEST-DEVICE",
    "EventType": "Success",
    "UserDisplayName": "Test User",
    "UserPrincipalName": "test@company.com",
    "OperatingSystem": "Windows",
    "OsVersion": "11"
  }'
```

## Monitoring

### Key Metrics to Monitor
1. **Notification Success Rate**: Track in Application Insights
2. **Function Execution Time**: Should be under 10 seconds typically
3. **Teams API Rate Limits**: Monitor for throttling
4. **Cost**: Review Azure Cost Management monthly

### Application Insights Queries

**Notification Success Rate:**
```kusto
customEvents
| where name == "EnrollmentEventProcessed"
| summarize SuccessCount = count() by bin(timestamp, 1h)
```

**Average Processing Time:**
```kusto
customEvents
| where name == "EnrollmentEventProcessed"
| extend Duration = todouble(customDimensions.ProcessingDurationMs)
| summarize avg(Duration) by bin(timestamp, 1h)
```

## Troubleshooting

### Teams Notifications Not Sending

**Check 1**: Verify Graph API permissions
```bash
# Test connection
GET https://your-function-app.azurewebsites.net/api/health
```

**Check 2**: Verify Team/Channel IDs are correct
- Team ID and Channel ID must be valid
- App must have access to the team

**Check 3**: Check Application Insights logs
```kusto
traces
| where message contains "Teams"
| order by timestamp desc
```

### Permission Errors

If you see "Forbidden" errors:
1. Verify admin consent was granted for Graph API permissions
2. Check that the app has access to the specific team
3. Ensure client secret hasn't expired

## Security Considerations

1. **Client Secret Rotation**: Rotate Graph API client secret every 90 days
2. **Key Vault Access**: Use Managed Identity for Key Vault access
3. **Function Authorization**: Keep function keys secure
4. **Least Privilege**: Only grant necessary Graph API permissions

## Cost Comparison

### Monthly Cost Estimate (100 enrollments/day)

**Email (SendGrid):**
- SendGrid Essentials: $19.95/month (40,000 emails)
- Azure Function: ~$5/month
- Storage: ~$2/month
- **Total: ~$27/month**

**Teams:**
- Teams: $0 (included in M365)
- Azure Function: ~$2/month (optimized)
- Storage: ~$2/month
- **Total: ~$4/month**

**Savings: ~$23/month (85% reduction)**

## Next Steps

1. Configure Azure AD app with required permissions
2. Get Team and Channel IDs
3. Update configuration with Teams settings
4. Test with "Both" mode first
5. Switch to "Teams" mode after validation
6. Monitor and optimize based on usage

## Support

For issues or questions:
1. Check Application Insights logs
2. Review health check endpoint
3. Verify configuration settings
4. Test Graph API connectivity

## References

- [Microsoft Graph API Documentation](https://docs.microsoft.com/en-us/graph/)
- [Adaptive Cards Documentation](https://adaptivecards.io/)
- [Azure Functions Best Practices](https://docs.microsoft.com/en-us/azure/azure-functions/functions-best-practices)
- [Azure Cost Management](https://docs.microsoft.com/en-us/azure/cost-management-billing/)
