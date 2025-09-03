# Project Context

## Purpose

**Intune Enrollment Notifier** is an Azure Function application that monitors Microsoft Intune device enrollments and sends real-time notifications to IT teams via Microsoft Teams or Email.

### Goals
- Provide real-time visibility into device enrollment events (success, failure, warnings)
- Enable rapid troubleshooting with detailed diagnostic information
- Reduce incident response time by 50% through proactive notifications
- Minimize operational costs (76-90% reduction with Teams vs Email)
- Support enterprise-scale enrollment volumes with high availability (99.9% uptime)

## Tech Stack

### Core Technologies
- **Language**: C# (.NET 6.0)
- **Runtime**: Azure Functions v4 (Consumption plan)
- **Cloud Platform**: Microsoft Azure
- **Notification Channels**: Microsoft Teams (Adaptive Cards), SendGrid Email

### Key Azure Services
- **Azure Functions**: Event processing and orchestration
- **Azure Key Vault**: Secure credential storage
- **Azure Storage Account**: Logs and event tracking
- **Application Insights**: Monitoring and telemetry
- **Azure Logic Apps**: Optional workflow orchestration

### NuGet Packages
- `Microsoft.NET.Sdk.Functions` (4.2.0) - Azure Functions SDK
- `Microsoft.Graph` (5.36.0) - Graph API integration
- `Microsoft.Graph.Auth` (1.0.0-preview.7) - Graph authentication
- `Azure.Identity` (1.10.4) - Azure authentication
- `Azure.Security.KeyVault.Secrets` (4.5.0) - Key Vault access
- `SendGrid` (9.28.1) - Email delivery (legacy)
- `Microsoft.ApplicationInsights` (2.21.0) - Telemetry
- `Newtonsoft.Json` (13.0.3) - JSON serialization

## Project Conventions

### Code Style
- **C# Conventions**: Follow standard .NET naming conventions
  - PascalCase for classes, methods, properties, public fields
  - camelCase for private fields, local variables, parameters
  - Async methods suffixed with `Async`
- **Nullable Reference Types**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled for cleaner code
- **File Organization**:
  - Services in `Services/` directory
  - Models in `Models/` directory
  - Functions at root level
- **Configuration**: Environment variables and Azure Key Vault secrets (never hardcode credentials)

### Architecture Patterns

#### Service-Oriented Architecture
- **GraphService**: Handles all Microsoft Graph API interactions
- **TeamsNotificationService**: Teams-specific notification logic
- **EmailService**: SendGrid email notification logic
- **Dependency Injection**: Services registered in `Startup.cs`

#### Event Processing Flow
```
Timer Trigger (5 min) → GraphService → Event Classification → Notification Router → Teams/Email
                                                                        ↓
                                                              Application Insights
```

#### Configuration-Based Routing
- Runtime switching between notification channels via `NOTIFICATION_TYPE` setting
- No code deployment required to change notification method
- Supports: "Teams", "Email", or "Both"

#### Security Patterns
- **Managed Identity**: For Azure service authentication
- **Azure Key Vault**: All secrets stored securely
- **Least Privilege**: Minimum required Graph API permissions
- **HTTPS Only**: All endpoints enforce HTTPS

### Testing Strategy

#### Current State
- Manual testing via health check endpoint
- Integration testing with real Graph API and Teams/Email services
- Application Insights for production monitoring

#### Planned Improvements
- Unit tests for service classes
- Integration tests with mocked Graph API
- End-to-end tests for notification delivery
- Performance testing for high-volume scenarios

#### Testing Endpoints
- **Health Check**: `GET /api/health` - Validates service connectivity
- **Manual Event**: `POST /api/ProcessEnrollmentEvent` - Trigger test notifications

### Git Workflow
- **Main Branch**: Production-ready code
- **Feature Branches**: For new capabilities and changes
- **OpenSpec Workflow**: Spec-driven development with proposal → implementation → archive cycle
- **Commit Messages**: Descriptive, reference change IDs when applicable
- **Documentation**: Keep README, specs, and deployment guides in sync with code

## Domain Context

### Microsoft Intune Concepts
- **Managed Devices**: Devices enrolled in Intune for management
- **Enrollment States**: Enrolled, Pending, Failed
- **Compliance States**: Compliant, NonCompliant, InGracePeriod, ConfigManager, Unknown
- **Device Types**: Windows, iOS, Android, macOS
- **Management Agents**: MDM, EAS, ConfigManager, Intune, etc.

### Event Types Monitored
1. **Success Events**: Successful enrollment, policy application
2. **Failure Events**: Authentication failures, certificate issues, policy conflicts
3. **Warning Events**: Compliance issues, partial policy application

### Notification Content
- Device information (name, OS, version, model, serial number)
- User context (UPN, display name, assigned policies)
- Error codes and descriptions (for failures)
- Diagnostic information and troubleshooting steps
- Timestamp and processing metadata

### Microsoft Graph API
- **Endpoint**: `/deviceManagement/managedDevices`
- **Permissions**: Application-level (not delegated)
- **Throttling**: Respect rate limits with exponential backoff
- **Authentication**: Client credentials flow with Azure AD app registration

## Important Constraints

### Technical Constraints
- **Performance**: < 7 minutes end-to-end processing time (typically < 2 minutes)
- **Availability**: 99.9% uptime requirement
- **Scalability**: Support up to 1000 enrollment events/hour
- **Graph API Limits**: Respect Microsoft Graph throttling limits
- **Function Timeout**: Optimized for quick execution (5-minute timer trigger)

### Security & Compliance
- **Data Protection**: Encryption in transit and at rest
- **PII Handling**: Minimize personal data exposure in logs
- **GDPR Compliance**: Configurable data retention policies
- **Secret Management**: All credentials in Azure Key Vault only
- **Authentication**: Azure AD app registration with least privilege permissions

### Cost Constraints
- **Target**: < $10/month operational cost
- **Optimization**: Prefer Teams over Email (76-90% cost reduction)
- **Consumption Plan**: Use serverless for cost efficiency

### Operational Constraints
- **Monitoring**: All events logged to Application Insights
- **Alerting**: Failed notifications must alert administrators
- **Retry Logic**: Exponential backoff for transient failures
- **Fallback**: Teams failures fall back to Email when configured as "Both"

## External Dependencies

### Microsoft Services
- **Microsoft Graph API**: Primary data source for Intune device data
  - Required permissions: DeviceManagementManagedDevices.Read.All, DeviceManagementConfiguration.Read.All, User.Read.All
  - Teams permissions: ChannelMessage.Send, Team.ReadBasic.All, Channel.ReadBasic.All
- **Microsoft Teams**: Notification delivery via Adaptive Cards
  - Requires: Team ID and Channel ID configuration
  - No additional cost (included in M365 licenses)
- **Azure Active Directory**: Authentication and authorization
  - App registration with client secret
  - Service principal for Graph API access

### Third-Party Services
- **SendGrid API**: Email notification delivery (legacy/optional)
  - API key stored in Key Vault
  - Cost varies by volume ($0-$100/month)
  - Used when NOTIFICATION_TYPE is "Email" or "Both"

### Azure Services
- **Azure Key Vault**: Secret storage (client secrets, API keys)
- **Azure Storage**: Function app storage and event logs
- **Application Insights**: Telemetry and monitoring
- **Azure Functions Runtime**: Serverless execution environment

### Development Tools
- **.NET 6.0 SDK**: Required for local development
- **Azure Functions Core Tools**: Local testing and deployment
- **Visual Studio Code / Visual Studio**: Recommended IDEs
- **Azure CLI / PowerShell**: Infrastructure deployment
