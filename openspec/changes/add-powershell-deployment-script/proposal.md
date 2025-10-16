# Proposal: Add PowerShell Deployment Script

## Change ID
`add-powershell-deployment-script`

## Status
**Pending Approval**

## Summary
Add a comprehensive PowerShell deployment script to automate the complete deployment of the Intune Enrollment Notifier solution, replacing manual Azure CLI steps with a single automated PowerShell script.

## Why

The current deployment process requires executing multiple manual Azure CLI and PowerShell commands across 9 separate steps. This approach:
- Is error-prone and time-consuming
- Requires switching between different tools (Azure CLI, PowerShell, .NET CLI)
- Makes it difficult to deploy consistently across environments
- Lacks validation and error handling between steps

A unified PowerShell deployment script will:
- Reduce deployment time from ~2 hours to ~15 minutes
- Ensure consistent deployments across dev/staging/prod environments
- Provide built-in validation and error handling
- Enable CI/CD automation
- Improve the deployment experience for IT administrators

## Proposed Changes

### New Files
1. **`Deploy-IntuneNotifier.ps1`** - Main deployment script with all automation
2. **`deployment-config.json`** - Configuration file for deployment parameters

### Modified Files
1. **`deployment-guide.md`** - Add PowerShell script usage instructions
2. **`README.md`** - Update quick start with PowerShell option

## Technical Approach

### Script Capabilities
The PowerShell script will handle:
1. **Azure AD App Registration** - Create app, service principal, and permissions
2. **Azure Resource Deployment** - Deploy all Azure resources via Bicep
3. **Key Vault Configuration** - Store all secrets securely
4. **Function App Configuration** - Set app settings and managed identity
5. **Function Code Deployment** - Build and deploy .NET code
6. **Validation & Testing** - Health check and connectivity tests
7. **Rollback Support** - Ability to rollback on failure
8. **Detailed Logging** - All activities logged to `Deploy-IntuneNotifier-YYYY-MM-DD-HH-MM.log` in Documents folder

### Parameters
- Environment (dev/staging/prod)
- Notification type (Teams/Email/Both)
- Teams configuration (Team ID, Channel ID)
- SendGrid configuration (optional)
- Resource naming and location

### Error Handling
- Pre-flight validation of prerequisites
- Checkpoint-based execution with resume capability
- Detailed error messages with remediation steps
- Automatic rollback on critical failures

## Acceptance Criteria
- [ ] PowerShell script successfully deploys all Azure resources
- [ ] Script handles both Teams and Email notification configurations
- [ ] Configuration file supports all deployment parameters
- [ ] Script includes comprehensive error handling and validation
- [ ] Deployment guide updated with PowerShell instructions
- [ ] README updated with quick start using PowerShell
- [ ] Script tested on fresh Azure subscription
- [ ] Rollback functionality works correctly

## Risks & Mitigation
- **Risk**: Script complexity may introduce bugs
  - **Mitigation**: Include extensive validation and testing
- **Risk**: Azure PowerShell module version compatibility
  - **Mitigation**: Document required versions and check at runtime
- **Risk**: Breaking existing Azure CLI workflow
  - **Mitigation**: Keep both options documented (PowerShell primary, CLI as alternative)

## Dependencies
- Azure PowerShell module (Az) version 8.0+
- AzureAD PowerShell module (for app registration)
- .NET 6.0 SDK (for building function app)
- Azure Functions Core Tools v4

## Timeline
- **Development**: 1 day
- **Testing**: 1 day
- **Documentation**: 0.5 days
- **Total**: 2.5 days

## Related Documentation
- `deployment-guide.md` - Current manual deployment process
- `azure-resources.bicep` - Infrastructure as code template
- `SETUP_TEAMS.md` - Teams configuration details
