# Deployment Automation

## ADDED Requirements

### REQ-DA-001: PowerShell Deployment Script

The system SHALL provide an automated PowerShell deployment script that deploys all Azure resources and configures the Intune Enrollment Notifier solution.

#### Scenario: Fresh deployment to new Azure subscription
**Given** an Azure subscription with no existing resources  
**When** an administrator runs the deployment script with valid parameters  
**Then** all required Azure resources are created  
**And** the Function App is deployed with the application code  
**And** all secrets are stored in Key Vault  
**And** the health check endpoint returns "Healthy"

#### Scenario: Deployment with Teams notification configuration
**Given** valid Teams Team ID and Channel ID  
**When** the script is executed with notification type "Teams"  
**Then** the Function App is configured with Teams settings  
**And** a test notification is sent to the Teams channel  
**And** the Adaptive Card appears correctly in Teams

### REQ-DA-002: Configuration Management

The system SHALL support configuration files for deployment parameters to enable repeatable deployments across environments.

#### Scenario: Deploy to multiple environments using config files
**Given** separate configuration files for dev, staging, and prod  
**When** the administrator runs the script specifying an environment  
**Then** the script loads the corresponding configuration file  
**And** resources are deployed with environment-specific settings  
**And** resource names include the environment identifier

### REQ-DA-003: Pre-flight Validation

The deployment script SHALL validate all prerequisites before attempting deployment.

#### Scenario: Missing required PowerShell modules
**Given** the Azure PowerShell module is not installed  
**When** the administrator runs the deployment script  
**Then** the script detects the missing module  
**And** provides clear instructions to install the module  
**And** exits without attempting deployment

#### Scenario: Invalid Azure credentials
**Given** the user is not authenticated to Azure  
**When** the deployment script attempts to check subscription access  
**Then** the script prompts for authentication  
**And** validates the user has required permissions  
**And** fails with a clear error if permissions are insufficient

### REQ-DA-004: Error Handling and Rollback

The deployment script SHALL handle errors gracefully and support rollback of failed deployments.

#### Scenario: Deployment failure during resource creation
**Given** a deployment is in progress  
**When** a resource creation fails (e.g., Key Vault name conflict)  
**Then** the script logs the error with details  
**And** offers to rollback successfully created resources  
**And** provides remediation steps to resolve the issue

#### Scenario: Resume after partial failure
**Given** a deployment failed at a specific step  
**When** the administrator resolves the issue and reruns the script  
**Then** the script detects existing resources  
**And** skips already completed steps  
**And** continues from the failure point

### REQ-DA-005: Progress Reporting and Logging

The deployment script SHALL provide clear progress indicators and logging throughout the deployment process. All deployment activity SHALL be logged to a timestamped file in the user's Documents folder with the format `Deploy-IntuneNotifier-YYYY-MM-DD-HH-MM.log`.

#### Scenario: Monitor deployment progress
**Given** a deployment is in progress  
**When** the script is executing various steps  
**Then** each major step displays a progress message  
**And** completion percentage is shown  
**And** estimated time remaining is displayed  
**And** all actions are logged to a deployment log file

#### Scenario: Log file creation with timestamp
**Given** the deployment script is starting  
**When** the script initializes logging  
**Then** a log file is created in the user's Documents folder  
**And** the filename follows the pattern `Deploy-IntuneNotifier-YYYY-MM-DD-HH-MM.log`  
**And** the log file contains timestamp for each log entry  
**And** the log file path is displayed to the user

### REQ-DA-006: Post-Deployment Validation

The deployment script SHALL validate the deployment by testing key functionality after completion.

#### Scenario: Validate successful deployment
**Given** all Azure resources have been created  
**When** the deployment script completes  
**Then** the health check endpoint is called and validates connectivity  
**And** Graph API connectivity is tested  
**And** notification service connectivity is verified  
**And** a test notification is sent  
**And** deployment summary with all endpoints is displayed
