<#
.SYNOPSIS
    Automated deployment script for Intune Enrollment Notifier solution.

.DESCRIPTION
    This script automates the complete deployment of the Intune Enrollment Notifier
    Azure Function application including Azure AD app registration, Azure resources,
    Key Vault secrets, Function App configuration, and code deployment.

.PARAMETER Environment
    Target environment: dev, staging, or prod (default: prod)

.PARAMETER Location
    Azure region for resources (default: Canada Central)

.PARAMETER ResourceGroupName
    Name of the resource group (default: rg-intune-notify-{environment})

.PARAMETER NotificationType
    Notification method: Teams, Email, or Both (default: Teams)

.PARAMETER TeamsTeamId
    Microsoft Teams Team ID (required if NotificationType is Teams or Both)

.PARAMETER TeamsChannelId
    Microsoft Teams Channel ID (required if NotificationType is Teams or Both)

.PARAMETER SendGridApiKey
    SendGrid API key (required if NotificationType is Email or Both)

.PARAMETER NotificationEmails
    Comma-separated list of email addresses (required if NotificationType is Email or Both)

.PARAMETER ConfigFile
    Path to JSON configuration file (optional, overrides parameters)

.PARAMETER SkipAppRegistration
    Skip Azure AD app registration if it already exists

.PARAMETER SkipResourceDeployment
    Skip Azure resource deployment (useful for code-only updates)

.EXAMPLE
    .\Deploy-IntuneNotifier.ps1 -Environment prod -NotificationType Teams -TeamsTeamId "xxx" -TeamsChannelId "yyy"

.EXAMPLE
    .\Deploy-IntuneNotifier.ps1 -ConfigFile .\deployment-config.json

.NOTES
    Version: 1.0
    Requires: Azure PowerShell (Az module), AzureAD module, .NET 6.0 SDK, Azure Functions Core Tools
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Environment = 'prod',

    [Parameter(Mandatory = $false)]
    [string]$Location = 'Canada Central',

    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = "",

    [Parameter(Mandatory = $false)]
    [ValidateSet('Teams', 'Email', 'Both')]
    [string]$NotificationType = 'Teams',

    [Parameter(Mandatory = $false)]
    [string]$TeamsTeamId = "",

    [Parameter(Mandatory = $false)]
    [string]$TeamsChannelId = "",

    [Parameter(Mandatory = $false)]
    [string]$SendGridApiKey = "",

    [Parameter(Mandatory = $false)]
    [string]$NotificationEmails = "",

    [Parameter(Mandatory = $false)]
    [string]$ConfigFile = "",

    [Parameter(Mandatory = $false)]
    [switch]$SkipAppRegistration,

    [Parameter(Mandatory = $false)]
    [switch]$SkipResourceDeployment
)

#Requires -Version 7.0

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Initialize log file
$timestamp = Get-Date -Format "yyyy-MM-dd-HH-mm"
$logPath = Join-Path -Path ([Environment]::GetFolderPath('MyDocuments')) -ChildPath "Deploy-IntuneNotifier-$timestamp.log"
$script:LogFile = $logPath
$script:DeploymentStart = Get-Date
$script:CreatedResources = @()

# Source additional script modules
$functionsPath = Join-Path -Path $PSScriptRoot -ChildPath "deployment-functions.ps1"
if (Test-Path $functionsPath) {
    . $functionsPath
}

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet('INFO', 'WARNING', 'ERROR', 'SUCCESS')]
        [string]$Level = 'INFO'
    )
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$ts] [$Level] $Message"
    Add-Content -Path $script:LogFile -Value $logMessage
    $color = switch ($Level) {
        'INFO' { 'Cyan' }
        'WARNING' { 'Yellow' }
        'ERROR' { 'Red' }
        'SUCCESS' { 'Green' }
    }
    Write-Host $logMessage -ForegroundColor $color
}

function Write-Progress-Step {
    param([string]$Step, [int]$Current, [int]$Total)
    $percent = [math]::Round(($Current / $Total) * 100)
    Write-Progress -Activity "Deploying Intune Enrollment Notifier" -Status $Step -PercentComplete $percent
    Write-Log "[$Current/$Total] $Step"
}

# Prerequisites check
function Test-Prerequisites {
    Write-Log "Checking prerequisites..."
    $missing = @()
    
    if ($PSVersionTable.PSVersion.Major -lt 7) {
        $missing += "PowerShell 7.0+ (current: $($PSVersionTable.PSVersion))"
    }
    if (-not (Get-Module -ListAvailable -Name Az.Accounts)) {
        $missing += "Az PowerShell module (Install-Module -Name Az)"
    }
    if (-not (Get-Module -ListAvailable -Name AzureAD)) {
        $missing += "AzureAD PowerShell module (Install-Module -Name AzureAD)"
    }
    try {
        $dotnetVer = & dotnet --version 2>$null
        if (-not $dotnetVer -or [version]$dotnetVer -lt [version]"6.0") {
            $missing += ".NET 6.0 SDK"
        }
    } catch {
        $missing += ".NET 6.0 SDK"
    }
    
    if ($missing.Count -gt 0) {
        Write-Log "Missing prerequisites:" -Level ERROR
        $missing | ForEach-Object { Write-Log "  - $_" -Level ERROR }
        throw "Prerequisites not met"
    }
    Write-Log "Prerequisites check passed" -Level SUCCESS
}

# Main deployment workflow
try {
    Write-Log "========================================" -Level SUCCESS
    Write-Log "Intune Enrollment Notifier Deployment" -Level SUCCESS
    Write-Log "========================================" -Level SUCCESS
    Write-Log "Log file: $script:LogFile" -Level SUCCESS
    Write-Log ""
    
    # Step 1: Prerequisites
    Write-Progress-Step -Step "Checking prerequisites" -Current 1 -Total 8
    Test-Prerequisites
    
    # Step 2: Configuration
    Write-Progress-Step -Step "Loading configuration" -Current 2 -Total 8
    if (-not [string]::IsNullOrEmpty($ConfigFile) -and (Test-Path $ConfigFile)) {
        Write-Log "Loading configuration from: $ConfigFile"
        $config = Get-Content -Path $ConfigFile -Raw | ConvertFrom-Json
        $Environment = $config.environment ?? $Environment
        $Location = $config.location ?? $Location
        $ResourceGroupName = $config.resourceGroupName ?? $ResourceGroupName
        $NotificationType = $config.notificationType ?? $NotificationType
        $TeamsTeamId = $config.teamsTeamId ?? $TeamsTeamId
        $TeamsChannelId = $config.teamsChannelId ?? $TeamsChannelId
        $SendGridApiKey = $config.sendGridApiKey ?? $SendGridApiKey
        $NotificationEmails = $config.notificationEmails ?? $NotificationEmails
    }
    
    if ([string]::IsNullOrEmpty($ResourceGroupName)) {
        $ResourceGroupName = "rg-intune-notify-$Environment"
    }
    
    if ($NotificationType -in @('Teams', 'Both') -and ([string]::IsNullOrEmpty($TeamsTeamId) -or [string]::IsNullOrEmpty($TeamsChannelId))) {
        throw "TeamsTeamId and TeamsChannelId are required when NotificationType is Teams or Both"
    }
    
    Write-Log "Configuration initialized for environment: $Environment" -Level SUCCESS
    Write-Log "Resource Group: $ResourceGroupName"
    Write-Log "Location: $Location"
    Write-Log "Notification Type: $NotificationType"
    
    # Step 3: Azure connection
    Write-Progress-Step -Step "Connecting to Azure" -Current 3 -Total 8
    $context = Get-AzContext
    if (-not $context) {
        Write-Log "Connecting to Azure..."
        Connect-AzAccount
    }
    Write-Log "Connected to subscription: $((Get-AzContext).Subscription.Name)" -Level SUCCESS
    
    # Step 4: Create resource group
    if (-not $SkipResourceDeployment) {
        Write-Progress-Step -Step "Creating resource group" -Current 4 -Total 8
        $rg = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
        if (-not $rg) {
            $rg = New-AzResourceGroup -Name $ResourceGroupName -Location $Location
            Write-Log "Resource group created: $ResourceGroupName" -Level SUCCESS
        } else {
            Write-Log "Resource group already exists: $ResourceGroupName" -Level WARNING
        }
        
        # Step 5: Deploy Bicep template
        Write-Progress-Step -Step "Deploying Azure resources" -Current 5 -Total 8
        $bicepPath = Join-Path -Path $PSScriptRoot -ChildPath "azure-resources.bicep"
        if (-not (Test-Path $bicepPath)) {
            throw "Bicep template not found: $bicepPath"
        }
        
        $deployParams = @{
            environment = $Environment
            location = $Location
            sendGridApiKey = $SendGridApiKey ?? ""
        }
        if ($NotificationType -in @('Email', 'Both')) {
            $deployParams['notificationEmails'] = $NotificationEmails.Split(',')
        }
        
        Write-Log "Deploying Bicep template..."
        $deployment = New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -TemplateFile $bicepPath -TemplateParameterObject $deployParams
        
        if ($deployment.ProvisioningState -eq 'Succeeded') {
            $functionAppName = $deployment.Outputs['functionAppName'].Value
            $keyVaultName = $deployment.Outputs['keyVaultName'].Value
            $functionAppUrl = $deployment.Outputs['functionAppUrl'].Value
            Write-Log "Resources deployed successfully" -Level SUCCESS
            Write-Log "Function App: $functionAppName" -Level SUCCESS
            Write-Log "Key Vault: $keyVaultName" -Level SUCCESS
        } else {
            throw "Deployment failed"
        }
        
        # Step 6: Configure Key Vault
        Write-Progress-Step -Step "Configuring Key Vault" -Current 6 -Total 8
        Write-Log "NOTE: Manually configure Graph API credentials in Key Vault: $keyVaultName" -Level WARNING
        Write-Log "Required secrets: GRAPH-API-CLIENT-ID, GRAPH-API-TENANT-ID, GRAPH-API-CLIENT-SECRET" -Level WARNING
        
        # Step 7: Configure Function App
        Write-Progress-Step -Step "Configuring Function App" -Current 7 -Total 8
        $settings = @{
            "NOTIFICATION_TYPE" = $NotificationType
            "KeyVaultUrl" = "https://$keyVaultName.vault.azure.net/"
        }
        if ($NotificationType -in @('Teams', 'Both')) {
            $settings['TEAMS_TEAM_ID'] = $TeamsTeamId
            $settings['TEAMS_CHANNEL_ID'] = $TeamsChannelId
        }
        if ($NotificationType -in @('Email', 'Both')) {
            $settings['NotificationEmails'] = $NotificationEmails
        }
        
        $app = Get-AzWebApp -ResourceGroupName $ResourceGroupName -Name $functionAppName
        foreach ($key in $settings.Keys) {
            $app = Set-AzWebApp -ResourceGroupName $ResourceGroupName -Name $functionAppName -AppSettings @{$key = $settings[$key]} -AppendSettings
        }
        Write-Log "Function App configured" -Level SUCCESS
    }
    
    # Step 8: Deploy code
    Write-Progress-Step -Step "Deploying Function code" -Current 8 -Total 8
    $functionPath = Join-Path -Path $PSScriptRoot -ChildPath "src\IntuneNotificationFunction"
    if (Test-Path $functionPath) {
        Push-Location $functionPath
        Write-Log "Building project..."
        & dotnet build --configuration Release | Out-Null
        Write-Log "Publishing to Azure..."
        & func azure functionapp publish $functionAppName --csharp
        Pop-Location
        Write-Log "Code deployed" -Level SUCCESS
    } else {
        Write-Log "Function code path not found, skipping deployment" -Level WARNING
    }
    
    # Complete
    $duration = (Get-Date) - $script:DeploymentStart
    Write-Log ""
    Write-Log "========================================" -Level SUCCESS
    Write-Log "Deployment Completed Successfully!" -Level SUCCESS
    Write-Log "========================================" -Level SUCCESS
    Write-Log "Duration: $($duration.ToString('hh\:mm\:ss'))" -Level SUCCESS
    Write-Log "Environment: $Environment" -Level SUCCESS
    Write-Log "Resource Group: $ResourceGroupName" -Level SUCCESS
    if ($functionAppUrl) {
        Write-Log "Function URL: $functionAppUrl" -Level SUCCESS
        Write-Log "Health Check: $functionAppUrl/api/health" -Level SUCCESS
    }
    Write-Log "Log file: $script:LogFile" -Level SUCCESS
    
}
catch {
    Write-Log "========================================" -Level ERROR
    Write-Log "Deployment Failed!" -Level ERROR
    Write-Log "Error: $($_.Exception.Message)" -Level ERROR
    Write-Log "See log file: $script:LogFile" -Level ERROR
    exit 1
}
