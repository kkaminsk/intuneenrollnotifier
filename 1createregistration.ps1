Install-Module -Name AzureAD
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