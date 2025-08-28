using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using IntuneNotificationFunction.Models;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using System.Text;

namespace IntuneNotificationFunction.Services
{
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;
        private readonly SecretClient _keyVaultClient;
        private ISendGridClient? _sendGridClient;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
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

        private async Task<ISendGridClient> GetSendGridClientAsync()
        {
            if (_sendGridClient != null)
                return _sendGridClient;

            try
            {
                var apiKey = await GetSecretAsync("SendGridApiKey") ?? _configuration["SENDGRID_API_KEY"];
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new InvalidOperationException("SendGrid API key is not configured");
                }

                _sendGridClient = new SendGridClient(apiKey);
                _logger.LogInformation("SendGrid client initialized successfully");
                return _sendGridClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SendGrid client");
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

        public async Task<bool> SendEnrollmentNotificationAsync(EnrollmentEvent enrollmentEvent)
        {
            try
            {
                var notificationData = CreateNotificationData(enrollmentEvent);
                return await SendEmailAsync(notificationData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send enrollment notification for device {DeviceName}", enrollmentEvent.DeviceName);
                return false;
            }
        }

        public async Task<bool> SendEmailAsync(NotificationData notificationData)
        {
            try
            {
                var client = await GetSendGridClientAsync();
                var from = new EmailAddress(notificationData.FromEmail, notificationData.FromName);
                var subject = notificationData.Subject;
                
                var msg = new SendGridMessage();
                msg.SetFrom(from);
                msg.SetSubject(subject);
                
                // Add recipients
                foreach (var recipient in notificationData.Recipients)
                {
                    if (IsValidEmail(recipient))
                    {
                        msg.AddTo(new EmailAddress(recipient));
                    }
                    else
                    {
                        _logger.LogWarning("Invalid email address: {Email}", recipient);
                    }
                }

                // Set content
                if (!string.IsNullOrEmpty(notificationData.PlainTextContent))
                {
                    msg.AddContent(MimeType.Text, notificationData.PlainTextContent);
                }
                
                if (!string.IsNullOrEmpty(notificationData.HtmlContent))
                {
                    msg.AddContent(MimeType.Html, notificationData.HtmlContent);
                }

                // Add custom headers for tracking
                msg.AddHeader("X-Intune-Notification", "true");
                msg.AddHeader("X-Priority", notificationData.Priority);
                
                // Add categories for SendGrid analytics
                foreach (var tag in notificationData.Tags)
                {
                    msg.AddCategory(tag);
                }

                var response = await client.SendEmailAsync(msg);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Email sent successfully to {RecipientCount} recipients. Subject: {Subject}", 
                        notificationData.Recipients.Count, subject);
                    return true;
                }
                else
                {
                    var responseBody = await response.Body.ReadAsStringAsync();
                    _logger.LogError("Failed to send email. Status: {StatusCode}, Response: {Response}", 
                        response.StatusCode, responseBody);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending email");
                return false;
            }
        }

        private NotificationData CreateNotificationData(EnrollmentEvent enrollmentEvent)
        {
            var recipients = GetNotificationRecipients();
            var fromEmail = _configuration["NOTIFICATION_EMAIL_FROM"] ?? "noreply@company.com";
            
            var notificationData = new NotificationData
            {
                Recipients = recipients,
                FromEmail = fromEmail,
                FromName = "Intune Enrollment Notifier",
                Priority = GetPriorityFromEventType(enrollmentEvent.EventType),
                Tags = new List<string> { "intune", "enrollment", enrollmentEvent.EventType.ToLower(), enrollmentEvent.OperatingSystem.ToLower() }
            };

            // Create subject
            var statusIcon = GetStatusIcon(enrollmentEvent.EventType);
            notificationData.Subject = $"[INTUNE] {statusIcon} Device Enrollment {enrollmentEvent.EventType.ToUpper()} - {enrollmentEvent.DeviceName}";

            // Create email content
            notificationData.HtmlContent = CreateHtmlContent(enrollmentEvent);
            notificationData.PlainTextContent = CreatePlainTextContent(enrollmentEvent);

            return notificationData;
        }

        private List<string> GetNotificationRecipients()
        {
            var recipients = new List<string>();
            var emailConfig = _configuration["NotificationEmails"];
            
            if (!string.IsNullOrEmpty(emailConfig))
            {
                recipients.AddRange(emailConfig.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(email => email.Trim())
                    .Where(email => IsValidEmail(email)));
            }

            if (recipients.Count == 0)
            {
                _logger.LogWarning("No valid notification email recipients configured");
            }

            return recipients;
        }

        private string GetPriorityFromEventType(string eventType)
        {
            return eventType switch
            {
                "Failure" => "High",
                "Warning" => "Normal",
                "Success" => "Low",
                _ => "Normal"
            };
        }

        private string GetStatusIcon(string eventType)
        {
            return eventType switch
            {
                "Success" => "✅",
                "Failure" => "❌",
                "Warning" => "⚠️",
                _ => "ℹ️"
            };
        }

        private string CreateHtmlContent(EnrollmentEvent enrollmentEvent)
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset='utf-8'>");
            html.AppendLine("    <title>Intune Enrollment Notification</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }");
            html.AppendLine("        .container { max-width: 800px; margin: 0 auto; background-color: white; border-radius: 8px; }");
            html.AppendLine("        .header { background-color: #0078d4; color: white; padding: 20px; border-radius: 8px 8px 0 0; }");
            html.AppendLine("        .content { padding: 20px; }");
            html.AppendLine("        .info-table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
            html.AppendLine("        .info-table th, .info-table td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }");
            html.AppendLine("        .info-table th { background-color: #f8f9fa; font-weight: 600; }");
            html.AppendLine("        .section { margin: 20px 0; }");
            html.AppendLine("        .diagnostic-info { background-color: #f8f9fa; padding: 15px; border-radius: 4px; white-space: pre-line; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("    <div class='container'>");
            
            // Header
            var statusIcon = GetStatusIcon(enrollmentEvent.EventType);
            html.AppendLine($"        <div class='header'>");
            html.AppendLine($"            <h1>{statusIcon} Device Enrollment {enrollmentEvent.EventType}</h1>");
            html.AppendLine($"            <p>Device: <strong>{enrollmentEvent.DeviceName}</strong></p>");
            html.AppendLine($"        </div>");
            
            // Content
            html.AppendLine("        <div class='content'>");
            html.AppendLine("            <h3>Event Summary</h3>");
            html.AppendLine("            <table class='info-table'>");
            html.AppendLine($"                <tr><th>Status</th><td>{enrollmentEvent.EventType}</td></tr>");
            html.AppendLine($"                <tr><th>Device Name</th><td>{enrollmentEvent.DeviceName}</td></tr>");
            html.AppendLine($"                <tr><th>User</th><td>{enrollmentEvent.UserDisplayName} ({enrollmentEvent.UserPrincipalName})</td></tr>");
            html.AppendLine($"                <tr><th>Platform</th><td>{enrollmentEvent.OperatingSystem} {enrollmentEvent.OsVersion}</td></tr>");
            html.AppendLine($"                <tr><th>Timestamp</th><td>{enrollmentEvent.ProcessedDateTime:yyyy-MM-dd HH:mm:ss} UTC</td></tr>");
            html.AppendLine("            </table>");

            if (!string.IsNullOrEmpty(enrollmentEvent.DiagnosticInfo))
            {
                html.AppendLine("            <h3>Diagnostic Information</h3>");
                html.AppendLine($"            <div class='diagnostic-info'>{enrollmentEvent.DiagnosticInfo}</div>");
            }

            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string CreatePlainTextContent(EnrollmentEvent enrollmentEvent)
        {
            var text = new StringBuilder();
            
            text.AppendLine("INTUNE ENROLLMENT NOTIFICATION");
            text.AppendLine("===============================");
            text.AppendLine();
            text.AppendLine($"Status: {enrollmentEvent.EventType}");
            text.AppendLine($"Device: {enrollmentEvent.DeviceName}");
            text.AppendLine($"User: {enrollmentEvent.UserDisplayName} ({enrollmentEvent.UserPrincipalName})");
            text.AppendLine($"Platform: {enrollmentEvent.OperatingSystem} {enrollmentEvent.OsVersion}");
            text.AppendLine($"Timestamp: {enrollmentEvent.ProcessedDateTime:yyyy-MM-dd HH:mm:ss} UTC");
            text.AppendLine();

            if (!string.IsNullOrEmpty(enrollmentEvent.DiagnosticInfo))
            {
                text.AppendLine("DIAGNOSTIC INFORMATION");
                text.AppendLine("---------------------");
                text.AppendLine(enrollmentEvent.DiagnosticInfo);
                text.AppendLine();
            }

            text.AppendLine("This is an automated notification from the Intune Enrollment Monitoring System.");

            return text.ToString();
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
