using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using IntuneNotificationFunction.Models;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;

namespace IntuneNotificationFunction.Services
{
    public class GraphService
    {
        private readonly ILogger<GraphService> _logger;
        private readonly IConfiguration _configuration;
        private GraphServiceClient? _graphServiceClient;
        private readonly SecretClient _keyVaultClient;

        public GraphService(ILogger<GraphService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            var keyVaultUrl = _configuration["KeyVaultUrl"];
            if (!string.IsNullOrEmpty(keyVaultUrl))
            {
                _keyVaultClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
            }
            else
            {
                throw new InvalidOperationException("KeyVaultUrl configuration is missing");
            }
        }

        private async Task<GraphServiceClient> GetGraphServiceClientAsync()
        {
            if (_graphServiceClient != null)
                return _graphServiceClient;

            try
            {
                var clientId = await GetSecretAsync("GRAPH-API-CLIENT-ID") ?? _configuration["GRAPH_API_CLIENT_ID"];
                var tenantId = await GetSecretAsync("GRAPH-API-TENANT-ID") ?? _configuration["GRAPH_API_TENANT_ID"];
                var clientSecret = await GetSecretAsync("GRAPH-API-CLIENT-SECRET");

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientSecret))
                {
                    throw new InvalidOperationException("Graph API credentials are not properly configured");
                }

                var app = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
                    .Build();

                var authProvider = new ClientCredentialProvider(app);
                _graphServiceClient = new GraphServiceClient(authProvider);

                _logger.LogInformation("Graph Service Client initialized successfully");
                return _graphServiceClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Graph Service Client");
                throw;
            }
        }

        private async Task<string?> GetSecretAsync(string secretName)
        {
            try
            {
                var secret = await _keyVaultClient.GetSecretAsync(secretName);
                return secret.Value.Value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve secret {SecretName} from Key Vault", secretName);
                return null;
            }
        }

        public async Task<List<EnrollmentEvent>> GetManagedDevicesAsync(DateTime? lastCheck = null)
        {
            try
            {
                var graphClient = await GetGraphServiceClientAsync();
                var devices = new List<EnrollmentEvent>();

                var request = graphClient.DeviceManagement.ManagedDevices.Request()
                    .Select("id,deviceName,userPrincipalName,operatingSystem,osVersion,deviceType,enrollmentState,complianceState,lastSyncDateTime,enrolledDateTime,serialNumber,manufacturer,model,emailAddress,userId,managementAgent,deviceEnrollmentType,deviceRegistrationState,managementState,azureADDeviceId,deviceCategoryDisplayName,isSupervised,isEncrypted,userDisplayName")
                    .Top(1000);

                if (lastCheck.HasValue)
                {
                    var filterDate = lastCheck.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                    request = request.Filter($"enrolledDateTime ge {filterDate} or lastSyncDateTime ge {filterDate}");
                }

                var response = await request.GetAsync();

                while (response?.CurrentPage != null)
                {
                    foreach (var device in response.CurrentPage)
                    {
                        var enrollmentEvent = MapManagedDeviceToEnrollmentEvent(device);
                        
                        // Determine event type based on device state
                        enrollmentEvent.EventType = DetermineEventType(device);
                        
                        // Add diagnostic information
                        await EnrichWithDiagnosticInfoAsync(enrollmentEvent, device);
                        
                        devices.Add(enrollmentEvent);
                    }

                    if (response.NextPageRequest != null)
                    {
                        response = await response.NextPageRequest.GetAsync();
                    }
                    else
                    {
                        break;
                    }
                }

                _logger.LogInformation("Retrieved {DeviceCount} managed devices from Graph API", devices.Count);
                return devices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve managed devices from Graph API");
                throw;
            }
        }

        public async Task<EnrollmentEvent?> GetManagedDeviceByIdAsync(string deviceId)
        {
            try
            {
                var graphClient = await GetGraphServiceClientAsync();
                var device = await graphClient.DeviceManagement.ManagedDevices[deviceId]
                    .Request()
                    .GetAsync();

                if (device == null)
                {
                    _logger.LogWarning("Device with ID {DeviceId} not found", deviceId);
                    return null;
                }

                var enrollmentEvent = MapManagedDeviceToEnrollmentEvent(device);
                enrollmentEvent.EventType = DetermineEventType(device);
                await EnrichWithDiagnosticInfoAsync(enrollmentEvent, device);

                return enrollmentEvent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve device {DeviceId} from Graph API", deviceId);
                throw;
            }
        }

        private EnrollmentEvent MapManagedDeviceToEnrollmentEvent(ManagedDevice device)
        {
            return new EnrollmentEvent
            {
                Id = device.Id ?? string.Empty,
                DeviceName = device.DeviceName ?? string.Empty,
                UserPrincipalName = device.UserPrincipalName ?? string.Empty,
                OperatingSystem = device.OperatingSystem ?? string.Empty,
                OsVersion = device.OsVersion ?? string.Empty,
                DeviceType = device.DeviceType?.ToString() ?? string.Empty,
                EnrollmentState = device.EnrollmentState?.ToString() ?? string.Empty,
                ComplianceState = device.ComplianceState?.ToString() ?? string.Empty,
                LastSyncDateTime = device.LastSyncDateTime?.DateTime,
                EnrolledDateTime = device.EnrolledDateTime?.DateTime,
                SerialNumber = device.SerialNumber ?? string.Empty,
                Manufacturer = device.Manufacturer ?? string.Empty,
                Model = device.Model ?? string.Empty,
                EmailAddress = device.EmailAddress ?? string.Empty,
                UserId = device.UserId ?? string.Empty,
                ManagementAgent = device.ManagementAgent?.ToString() ?? string.Empty,
                DeviceEnrollmentType = device.DeviceEnrollmentType?.ToString() ?? string.Empty,
                DeviceRegistrationState = device.DeviceRegistrationState?.ToString() ?? string.Empty,
                ManagementState = device.ManagementState?.ToString() ?? string.Empty,
                AzureADDeviceId = device.AzureADDeviceId ?? string.Empty,
                DeviceCategoryDisplayName = device.DeviceCategoryDisplayName ?? string.Empty,
                IsSupervised = device.IsSupervised ?? false,
                IsEncrypted = device.IsEncrypted ?? false,
                UserDisplayName = device.UserDisplayName ?? string.Empty,
                ProcessedDateTime = DateTime.UtcNow
            };
        }

        private string DetermineEventType(ManagedDevice device)
        {
            // Determine event type based on device state
            if (device.ComplianceState == Microsoft.Graph.ComplianceState.Compliant && 
                device.EnrollmentState == Microsoft.Graph.EnrollmentState.Enrolled)
            {
                return "Success";
            }
            else if (device.ComplianceState == Microsoft.Graph.ComplianceState.Noncompliant ||
                     device.EnrollmentState == Microsoft.Graph.EnrollmentState.Failed)
            {
                return "Failure";
            }
            else if (device.EnrollmentState == Microsoft.Graph.EnrollmentState.Pending ||
                     device.ComplianceState == Microsoft.Graph.ComplianceState.InGracePeriod)
            {
                return "Warning";
            }

            return "Unknown";
        }

        private async Task EnrichWithDiagnosticInfoAsync(EnrollmentEvent enrollmentEvent, ManagedDevice device)
        {
            try
            {
                var diagnosticInfo = new List<string>();

                // Add basic device information
                diagnosticInfo.Add($"Device ID: {device.Id}");
                diagnosticInfo.Add($"Azure AD Device ID: {device.AzureADDeviceId}");
                diagnosticInfo.Add($"Management Agent: {device.ManagementAgent}");
                diagnosticInfo.Add($"Enrollment Type: {device.DeviceEnrollmentType}");
                diagnosticInfo.Add($"Registration State: {device.DeviceRegistrationState}");

                // Add compliance and enrollment details
                if (device.ComplianceState != null)
                {
                    diagnosticInfo.Add($"Compliance State: {device.ComplianceState}");
                }

                if (device.EnrollmentState != null)
                {
                    diagnosticInfo.Add($"Enrollment State: {device.EnrollmentState}");
                }

                // Add sync information
                if (device.LastSyncDateTime.HasValue)
                {
                    var timeSinceSync = DateTime.UtcNow - device.LastSyncDateTime.Value.DateTime;
                    diagnosticInfo.Add($"Last Sync: {device.LastSyncDateTime.Value.DateTime:yyyy-MM-dd HH:mm:ss} UTC ({timeSinceSync.TotalHours:F1} hours ago)");
                }

                enrollmentEvent.DiagnosticInfo = string.Join("\n", diagnosticInfo);

                // Add troubleshooting steps based on event type
                enrollmentEvent.TroubleshootingSteps = GenerateTroubleshootingSteps(enrollmentEvent, device);

                // Try to get additional policy information
                await GetDevicePolicyInfoAsync(enrollmentEvent, device.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich diagnostic info for device {DeviceId}", device.Id);
            }
        }

        private string GenerateTroubleshootingSteps(EnrollmentEvent enrollmentEvent, ManagedDevice device)
        {
            var steps = new List<string>();

            switch (enrollmentEvent.EventType)
            {
                case "Failure":
                    steps.Add("1. Check device connectivity to the internet");
                    steps.Add("2. Verify user has appropriate licenses assigned");
                    steps.Add("3. Check if device meets minimum requirements");
                    steps.Add("4. Review enrollment restrictions and device type restrictions");
                    steps.Add("5. Check Azure AD device registration status");
                    
                    if (device.ComplianceState == Microsoft.Graph.ComplianceState.Noncompliant)
                    {
                        steps.Add("6. Review compliance policy settings and requirements");
                        steps.Add("7. Check device compliance status in Intune portal");
                    }
                    break;

                case "Warning":
                    steps.Add("1. Monitor device for enrollment completion");
                    steps.Add("2. Check if user action is required on the device");
                    steps.Add("3. Verify network connectivity and proxy settings");
                    break;

                case "Success":
                    steps.Add("1. Verify all required apps and policies are deployed");
                    steps.Add("2. Confirm device compliance status");
                    steps.Add("3. Check that user can access corporate resources");
                    break;
            }

            return string.Join("\n", steps);
        }

        private async Task GetDevicePolicyInfoAsync(EnrollmentEvent enrollmentEvent, string? deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return;

            try
            {
                var graphClient = await GetGraphServiceClientAsync();
                
                // Get device configuration states
                var configStates = await graphClient.DeviceManagement.ManagedDevices[deviceId]
                    .DeviceConfigurationStates
                    .Request()
                    .GetAsync();

                var appliedPolicies = new List<string>();
                var failedPolicies = new List<string>();

                foreach (var state in configStates.CurrentPage)
                {
                    if (state.State == Microsoft.Graph.ComplianceStatus.Compliant)
                    {
                        appliedPolicies.Add(state.DisplayName ?? "Unknown Policy");
                    }
                    else if (state.State == Microsoft.Graph.ComplianceStatus.Noncompliant || 
                             state.State == Microsoft.Graph.ComplianceStatus.Error)
                    {
                        failedPolicies.Add($"{state.DisplayName ?? "Unknown Policy"} (State: {state.State})");
                    }
                }

                enrollmentEvent.AppliedPolicies = appliedPolicies;
                enrollmentEvent.FailedPolicies = failedPolicies;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get policy information for device {DeviceId}", deviceId);
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var graphClient = await GetGraphServiceClientAsync();
                var me = await graphClient.Me.Request().GetAsync();
                _logger.LogInformation("Graph API connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Graph API connection test failed");
                return false;
            }
        }
    }
}
