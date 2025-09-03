# Intune Enrollment Notifier

Azure Function application that monitors Microsoft Intune device enrollments and sends notifications via Microsoft Teams or Email (SendGrid).

## Overview

This application automatically detects new device enrollments, enrollment failures, and compliance issues in Microsoft Intune, then sends formatted notifications to your team. It supports both Microsoft Teams (recommended) and email notifications.

## Features

- ✅ **Real-time enrollment monitoring** via Microsoft Graph API
- ✅ **Multiple notification channels**: Teams, Email, or Both
- ✅ **Rich notifications** with Adaptive Cards in Teams
- ✅ **Detailed diagnostic information** for troubleshooting
- ✅ **Automatic event categorization** (Success, Warning, Failure)
- ✅ **Secure credential management** via Azure Key Vault
- ✅ **Cost-optimized** architecture (76-90% cost reduction with Teams)
- ✅ **Health check endpoint** for monitoring
- ✅ **Application Insights** integration for telemetry

## Architecture

```
Microsoft Intune → Graph API → Azure Function → Teams/Email
                                      ↓
                              Azure Key Vault
                                      ↓
                           Application Insights
```

## Cost Optimization

**Teams Integration Benefits:**
- **Eliminate SendGrid costs**: $0-$100/month savings
- **Reduce compute costs**: Optimized function timeout
- **Total savings**: 76-90% reduction in operational costs

See [COST_OPTIMIZATION.md](COST_OPTIMIZATION.md) for detailed analysis.

## Quick Start

### Prerequisites

- Azure subscription
- Microsoft 365 with Teams
- Azure AD admin access
- .NET 6.0 SDK

### Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/intuneenrollnotifier.git
   cd intuneenrollnotifier
   ```

2. **Configure Teams integration** (Recommended)
   
   Follow the step-by-step guide: [SETUP_TEAMS.md](SETUP_TEAMS.md)

3. **Deploy to Azure**
   ```bash
   # Build the project
   cd src/IntuneNotificationFunction
   dotnet build
   
   # Publish to Azure (replace with your function app name)
   func azure functionapp publish your-function-app-name
   ```

4. **Configure settings**
   - Update Azure Function App settings
   - Configure Key Vault secrets
   - Set notification type (Teams/Email/Both)

## Configuration

### Notification Types

Set `NOTIFICATION_TYPE` in Azure Function configuration:

- **`Teams`**: Microsoft Teams notifications only (recommended)
- **`Email`**: SendGrid email notifications only
- **`Both`**: Send to both channels (useful for testing)

### Required Settings

**Azure Function App Settings:**
```
NOTIFICATION_TYPE=Teams
TEAMS_TEAM_ID=your-team-id
TEAMS_CHANNEL_ID=your-channel-id
KeyVaultUrl=https://your-keyvault.vault.azure.net/
```

**Azure Key Vault Secrets:**
```
GRAPH-API-CLIENT-ID
GRAPH-API-TENANT-ID
GRAPH-API-CLIENT-SECRET
```

See [SETUP_TEAMS.md](SETUP_TEAMS.md) for detailed configuration instructions.

## Documentation

- **[SETUP_TEAMS.md](SETUP_TEAMS.md)** - Step-by-step Teams integration setup
- **[TEAMS_INTEGRATION.md](TEAMS_INTEGRATION.md)** - Detailed Teams integration guide
- **[COST_OPTIMIZATION.md](COST_OPTIMIZATION.md)** - Cost analysis and optimization strategies

## Usage

### Health Check

```bash
curl https://your-function-app.azurewebsites.net/api/health
```

Response:
```json
{
  "Status": "Healthy",
  "Services": {
    "GraphService": "Connected",
    "NotificationService": "Teams Connected"
  }
}
```

### Manual Event Processing

```bash
curl -X POST https://your-function-app.azurewebsites.net/api/ProcessEnrollmentEvent \
  -H "Content-Type: application/json" \
  -H "x-functions-key: YOUR_FUNCTION_KEY" \
  -d '{
    "DeviceName": "LAPTOP-001",
    "EventType": "Success",
    "UserDisplayName": "John Doe",
    "UserPrincipalName": "john.doe@company.com",
    "OperatingSystem": "Windows",
    "OsVersion": "11"
  }'
```

### Automatic Monitoring

The function automatically monitors enrollments every 5 minutes via timer trigger.

## Notification Examples

### Teams Notification

Teams notifications use Adaptive Cards with:
- Status icon (✅ Success, ❌ Failure, ⚠️ Warning)
- Device information
- User details
- Diagnostic information
- Troubleshooting steps
- Color-coded status

### Email Notification

Email notifications include:
- HTML formatted content
- Plain text fallback
- Device details
- Diagnostic information
- Professional styling

## Development

### Local Development

1. **Install dependencies**
   ```bash
   cd src/IntuneNotificationFunction
   dotnet restore
   ```

2. **Update local.settings.json**
   ```json
   {
     "Values": {
       "NOTIFICATION_TYPE": "Teams",
       "TEAMS_TEAM_ID": "your-team-id",
       "TEAMS_CHANNEL_ID": "your-channel-id",
       "GRAPH_API_CLIENT_ID": "your-client-id",
       "GRAPH_API_TENANT_ID": "your-tenant-id"
     }
   }
   ```

3. **Run locally**
   ```bash
   func start
   ```

### Project Structure

```
intuneenrollnotifier/
├── src/
│   └── IntuneNotificationFunction/
│       ├── Services/
│       │   ├── GraphService.cs           # Microsoft Graph API integration
│       │   ├── EmailService.cs           # SendGrid email service
│       │   └── TeamsNotificationService.cs # Teams notification service
│       ├── Models/
│       │   └── EnrollmentEvent.cs        # Event data models
│       ├── ProcessEnrollmentEvent.cs     # Main function logic
│       ├── Startup.cs                    # Dependency injection
│       ├── host.json                     # Function host configuration
│       └── local.settings.json           # Local development settings
├── SETUP_TEAMS.md                        # Teams setup guide
├── TEAMS_INTEGRATION.md                  # Teams integration documentation
├── COST_OPTIMIZATION.md                  # Cost optimization guide
└── README.md                             # This file
```

## Monitoring

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

**Error Analysis:**
```kusto
exceptions
| where timestamp > ago(24h)
| summarize count() by type, outerMessage
| order by count_ desc
```

## Troubleshooting

### Teams Notifications Not Working

1. Verify Graph API permissions are granted
2. Check Team/Channel IDs are correct
3. Ensure client secret hasn't expired
4. Review Application Insights logs

### Email Notifications Not Working

1. Verify SendGrid API key is valid
2. Check recipient email addresses
3. Review SendGrid dashboard for delivery status

### Function Not Triggering

1. Check timer trigger schedule in code
2. Verify Function App is running
3. Review Application Insights for errors

See [TEAMS_INTEGRATION.md](TEAMS_INTEGRATION.md) for detailed troubleshooting.

## Security

- ✅ All secrets stored in Azure Key Vault
- ✅ Managed Identity support
- ✅ Application-level Graph API permissions
- ✅ Function key authentication
- ✅ No secrets in source control

## Cost Breakdown

### Monthly Costs (Estimated)

| Configuration | Cost | Notes |
|--------------|------|-------|
| Teams (Recommended) | $8/month | 76-90% savings |
| Email (SendGrid) | $13-$113/month | Varies by volume |

See [COST_OPTIMIZATION.md](COST_OPTIMIZATION.md) for detailed analysis.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

[Your License Here]

## Support

For issues or questions:
1. Check documentation in this repository
2. Review Application Insights logs
3. Test health check endpoint
4. Create an issue in GitHub

## Roadmap

- [ ] Add support for multiple Teams channels
- [ ] Implement notification filtering/rules
- [ ] Add Slack integration
- [ ] Create deployment templates (ARM/Bicep)
- [ ] Add unit tests
- [ ] Implement retry logic for failed notifications

## Acknowledgments

- Microsoft Graph API
- Azure Functions
- Microsoft Teams
- SendGrid (legacy support)

## Version History

- **v2.0** - Teams integration, cost optimization
- **v1.0** - Initial release with email notifications

---

**Need help?** Check out:
- [Setup Guide](SETUP_TEAMS.md)
- [Teams Integration](TEAMS_INTEGRATION.md)
- [Cost Optimization](COST_OPTIMIZATION.md)