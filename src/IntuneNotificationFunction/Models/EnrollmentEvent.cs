using System.Text.Json.Serialization;

namespace IntuneNotificationFunction.Models
{
    public class EnrollmentEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; } = string.Empty;

        [JsonPropertyName("userPrincipalName")]
        public string UserPrincipalName { get; set; } = string.Empty;

        [JsonPropertyName("operatingSystem")]
        public string OperatingSystem { get; set; } = string.Empty;

        [JsonPropertyName("osVersion")]
        public string OsVersion { get; set; } = string.Empty;

        [JsonPropertyName("deviceType")]
        public string DeviceType { get; set; } = string.Empty;

        [JsonPropertyName("enrollmentState")]
        public string EnrollmentState { get; set; } = string.Empty;

        [JsonPropertyName("complianceState")]
        public string ComplianceState { get; set; } = string.Empty;

        [JsonPropertyName("lastSyncDateTime")]
        public DateTime? LastSyncDateTime { get; set; }

        [JsonPropertyName("enrolledDateTime")]
        public DateTime? EnrolledDateTime { get; set; }

        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; set; } = string.Empty;

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("emailAddress")]
        public string EmailAddress { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("managementAgent")]
        public string ManagementAgent { get; set; } = string.Empty;

        [JsonPropertyName("deviceEnrollmentType")]
        public string DeviceEnrollmentType { get; set; } = string.Empty;

        [JsonPropertyName("deviceRegistrationState")]
        public string DeviceRegistrationState { get; set; } = string.Empty;

        [JsonPropertyName("managementState")]
        public string ManagementState { get; set; } = string.Empty;

        [JsonPropertyName("azureADDeviceId")]
        public string AzureADDeviceId { get; set; } = string.Empty;

        [JsonPropertyName("deviceCategoryDisplayName")]
        public string DeviceCategoryDisplayName { get; set; } = string.Empty;

        [JsonPropertyName("isSupervised")]
        public bool IsSupervised { get; set; }

        [JsonPropertyName("isEncrypted")]
        public bool IsEncrypted { get; set; }

        [JsonPropertyName("userDisplayName")]
        public string UserDisplayName { get; set; } = string.Empty;

        [JsonPropertyName("configurationManagerClientEnabledFeatures")]
        public object? ConfigurationManagerClientEnabledFeatures { get; set; }

        // Additional properties for error tracking
        public string? ErrorCode { get; set; }
        public string? ErrorDescription { get; set; }
        public string? DiagnosticInfo { get; set; }
        public DateTime ProcessedDateTime { get; set; } = DateTime.UtcNow;
        public string EventType { get; set; } = "Unknown"; // Success, Failure, Warning
        public string? TroubleshootingSteps { get; set; }
        public string? NetworkInfo { get; set; }
        public List<string> AppliedPolicies { get; set; } = new List<string>();
        public List<string> FailedPolicies { get; set; } = new List<string>();
    }

    public class GraphApiResponse<T>
    {
        [JsonPropertyName("@odata.context")]
        public string? Context { get; set; }

        [JsonPropertyName("@odata.count")]
        public int? Count { get; set; }

        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = new List<T>();

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }

    public class NotificationData
    {
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string PlainTextContent { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new List<string>();
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "Intune Notification System";
        public Dictionary<string, string> TemplateData { get; set; } = new Dictionary<string, string>();
        public string Priority { get; set; } = "Normal"; // High, Normal, Low
        public List<string> Tags { get; set; } = new List<string>();
    }

    public class EnrollmentEventProcessingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public EnrollmentEvent? ProcessedEvent { get; set; }
        public NotificationData? NotificationData { get; set; }
        public DateTime ProcessingTimestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan ProcessingDuration { get; set; }
        public string? DiagnosticInfo { get; set; }
    }
}
