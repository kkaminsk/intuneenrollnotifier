using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using IntuneNotificationFunction.Models;
using IntuneNotificationFunction.Services;
using System.IO;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace IntuneNotificationFunction
{
    public class ProcessEnrollmentEvent
    {
        private readonly GraphService _graphService;
        private readonly EmailService _emailService;
        private readonly TeamsNotificationService _teamsService;
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<ProcessEnrollmentEvent> _logger;
        private readonly IConfiguration _configuration;

        public ProcessEnrollmentEvent(
            GraphService graphService,
            EmailService emailService,
            TeamsNotificationService teamsService,
            TelemetryClient telemetryClient,
            ILogger<ProcessEnrollmentEvent> logger,
            IConfiguration configuration)
        {
            _graphService = graphService;
            _emailService = emailService;
            _teamsService = teamsService;
            _telemetryClient = telemetryClient;
            _logger = logger;
            _configuration = configuration;
        }

        [FunctionName("ProcessEnrollmentEvent")]
        public async Task<EnrollmentEventProcessingResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new EnrollmentEventProcessingResult();

            try
            {
                _logger.LogInformation("Processing enrollment event started");

                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                
                if (string.IsNullOrEmpty(requestBody))
                {
                    _logger.LogWarning("Empty request body received");
                    result.Success = false;
                    result.ErrorMessage = "Empty request body";
                    return result;
                }

                // Deserialize the enrollment event
                EnrollmentEvent? enrollmentEvent;
                try
                {
                    enrollmentEvent = JsonConvert.DeserializeObject<EnrollmentEvent>(requestBody);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize enrollment event from request body");
                    result.Success = false;
                    result.ErrorMessage = $"Invalid JSON format: {ex.Message}";
                    return result;
                }

                if (enrollmentEvent == null)
                {
                    _logger.LogWarning("Deserialized enrollment event is null");
                    result.Success = false;
                    result.ErrorMessage = "Invalid enrollment event data";
                    return result;
                }

                // Process the enrollment event
                var processedEvent = await ProcessEnrollmentEventAsync(enrollmentEvent);
                
                if (processedEvent == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to process enrollment event";
                    return result;
                }

                // Send notification
                var notificationSent = await SendNotificationAsync(processedEvent);
                
                if (!notificationSent)
                {
                    _logger.LogWarning("Failed to send notification for device {DeviceName}", processedEvent.DeviceName);
                    result.Success = false;
                    result.ErrorMessage = "Failed to send notification";
                    return result;
                }

                // Success
                result.Success = true;
                result.ProcessedEvent = processedEvent;
                result.ProcessingDuration = stopwatch.Elapsed;

                _logger.LogInformation("Successfully processed enrollment event for device {DeviceName} in {Duration}ms", 
                    processedEvent.DeviceName, stopwatch.ElapsedMilliseconds);

                // Track telemetry
                _telemetryClient.TrackEvent("EnrollmentEventProcessed", new Dictionary<string, string>
                {
                    ["DeviceName"] = processedEvent.DeviceName,
                    ["EventType"] = processedEvent.EventType,
                    ["OperatingSystem"] = processedEvent.OperatingSystem,
                    ["ProcessingDurationMs"] = stopwatch.ElapsedMilliseconds.ToString()
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing enrollment event");
                
                result.Success = false;
                result.ErrorMessage = $"Unexpected error: {ex.Message}";
                result.ProcessingDuration = stopwatch.Elapsed;

                // Track error telemetry
                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    ["FunctionName"] = "ProcessEnrollmentEvent",
                    ["ProcessingDurationMs"] = stopwatch.ElapsedMilliseconds.ToString()
                });

                return result;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        [FunctionName("MonitorEnrollmentEvents")]
        public async Task MonitorEnrollmentEvents(
            [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, // Every 5 minutes
            ILogger log)
        {
            try
            {
                log.LogInformation("Monitoring enrollment events started at: {time}", DateTime.Now);

                // Get devices enrolled in the last 10 minutes
                var lastCheck = DateTime.UtcNow.AddMinutes(-10);
                var devices = await _graphService.GetManagedDevicesAsync(lastCheck);

                log.LogInformation("Found {DeviceCount} devices to process", devices.Count);

                foreach (var device in devices)
                {
                    try
                    {
                        var processedEvent = await ProcessEnrollmentEventAsync(device);
                        if (processedEvent != null)
                        {
                            await SendNotificationAsync(processedEvent);
                            log.LogInformation("Processed and notified for device {DeviceName}", device.DeviceName);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Failed to process device {DeviceName}", device.DeviceName);
                        
                        // Track individual device processing errors
                        _telemetryClient.TrackException(ex, new Dictionary<string, string>
                        {
                            ["DeviceName"] = device.DeviceName,
                            ["DeviceId"] = device.Id,
                            ["FunctionName"] = "MonitorEnrollmentEvents"
                        });
                    }
                }

                log.LogInformation("Monitoring enrollment events completed");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in MonitorEnrollmentEvents function");
                _telemetryClient.TrackException(ex);
            }
        }

        [FunctionName("HealthCheck")]
        public async Task<IActionResult> HealthCheck(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req,
            ILogger log)
        {
            try
            {
                var healthStatus = new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0",
                    Services = new
                    {
                        GraphService = await TestGraphServiceAsync(),
                        NotificationService = await TestNotificationServiceAsync()
                    }
                };

                return new OkObjectResult(healthStatus);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Health check failed");
                
                var healthStatus = new
                {
                    Status = "Unhealthy",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message
                };

                return new ObjectResult(healthStatus) { StatusCode = 500 };
            }
        }

        private async Task<EnrollmentEvent?> ProcessEnrollmentEventAsync(EnrollmentEvent enrollmentEvent)
        {
            try
            {
                // If the event doesn't have an ID, it might be a webhook payload
                // In that case, we need to fetch the full device details
                if (string.IsNullOrEmpty(enrollmentEvent.Id) && !string.IsNullOrEmpty(enrollmentEvent.DeviceName))
                {
                    _logger.LogInformation("Fetching full device details for {DeviceName}", enrollmentEvent.DeviceName);
                    // This would require additional Graph API calls to find the device by name
                    // For now, we'll work with the provided data
                }

                // Enrich the event with additional diagnostic information
                await EnrichEnrollmentEventAsync(enrollmentEvent);

                // Determine if this is a significant event that requires notification
                if (ShouldSendNotification(enrollmentEvent))
                {
                    _logger.LogInformation("Event qualifies for notification: {DeviceName} - {EventType}", 
                        enrollmentEvent.DeviceName, enrollmentEvent.EventType);
                    return enrollmentEvent;
                }
                else
                {
                    _logger.LogInformation("Event does not qualify for notification: {DeviceName} - {EventType}", 
                        enrollmentEvent.DeviceName, enrollmentEvent.EventType);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process enrollment event for device {DeviceName}", enrollmentEvent.DeviceName);
                return null;
            }
        }

        private async Task EnrichEnrollmentEventAsync(EnrollmentEvent enrollmentEvent)
        {
            try
            {
                // Add processing timestamp
                enrollmentEvent.ProcessedDateTime = DateTime.UtcNow;

                // If we have a device ID, fetch additional details from Graph API
                if (!string.IsNullOrEmpty(enrollmentEvent.Id))
                {
                    var deviceDetails = await _graphService.GetManagedDeviceByIdAsync(enrollmentEvent.Id);
                    if (deviceDetails != null)
                    {
                        // Merge additional details
                        enrollmentEvent.DiagnosticInfo = deviceDetails.DiagnosticInfo;
                        enrollmentEvent.TroubleshootingSteps = deviceDetails.TroubleshootingSteps;
                        enrollmentEvent.AppliedPolicies = deviceDetails.AppliedPolicies;
                        enrollmentEvent.FailedPolicies = deviceDetails.FailedPolicies;
                    }
                }

                // Add network information if available
                // This could be enhanced to include more network details
                enrollmentEvent.NetworkInfo = "Network information collection not implemented";

                _logger.LogDebug("Enriched enrollment event for device {DeviceName}", enrollmentEvent.DeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich enrollment event for device {DeviceName}", enrollmentEvent.DeviceName);
                // Don't fail the entire process if enrichment fails
            }
        }

        private bool ShouldSendNotification(EnrollmentEvent enrollmentEvent)
        {
            // Always notify for failures
            if (enrollmentEvent.EventType == "Failure")
            {
                return true;
            }

            // Notify for warnings if they're recent
            if (enrollmentEvent.EventType == "Warning")
            {
                return true;
            }

            // Notify for successful enrollments (can be configured)
            if (enrollmentEvent.EventType == "Success")
            {
                // Could add configuration to control success notifications
                return true;
            }

            // Default to not sending notification
            return false;
        }

        private async Task<string> TestGraphServiceAsync()
        {
            try
            {
                var isConnected = await _graphService.TestConnectionAsync();
                return isConnected ? "Connected" : "Disconnected";
            }
            catch
            {
                return "Error";
            }
        }

        private async Task<string> TestNotificationServiceAsync()
        {
            try
            {
                var notificationType = _configuration["NOTIFICATION_TYPE"] ?? "Email";
                
                if (notificationType.Equals("Teams", StringComparison.OrdinalIgnoreCase))
                {
                    var isConnected = await _teamsService.TestConnectionAsync();
                    return isConnected ? "Teams Connected" : "Teams Disconnected";
                }
                else
                {
                    return "Email Available";
                }
            }
            catch
            {
                return "Error";
            }
        }

        private async Task<bool> SendNotificationAsync(EnrollmentEvent enrollmentEvent)
        {
            try
            {
                var notificationType = _configuration["NOTIFICATION_TYPE"] ?? "Email";
                
                if (notificationType.Equals("Teams", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Sending Teams notification for device {DeviceName}", enrollmentEvent.DeviceName);
                    return await _teamsService.SendEnrollmentNotificationAsync(enrollmentEvent);
                }
                else if (notificationType.Equals("Both", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Sending both Teams and Email notifications for device {DeviceName}", enrollmentEvent.DeviceName);
                    var teamsResult = await _teamsService.SendEnrollmentNotificationAsync(enrollmentEvent);
                    var emailResult = await _emailService.SendEnrollmentNotificationAsync(enrollmentEvent);
                    return teamsResult || emailResult; // Success if at least one succeeds
                }
                else
                {
                    _logger.LogInformation("Sending Email notification for device {DeviceName}", enrollmentEvent.DeviceName);
                    return await _emailService.SendEnrollmentNotificationAsync(enrollmentEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for device {DeviceName}", enrollmentEvent.DeviceName);
                return false;
            }
        }
    }
}
