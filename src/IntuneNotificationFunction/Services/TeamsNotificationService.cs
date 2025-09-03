using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using IntuneNotificationFunction.Models;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using System.Text;

namespace IntuneNotificationFunction.Services
{
    public class TeamsNotificationService
    {
        private readonly ILogger<TeamsNotificationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly SecretClient _keyVaultClient;
        private GraphServiceClient? _graphServiceClient;

        public TeamsNotificationService(ILogger<TeamsNotificationService> logger, IConfiguration configuration)
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

                _logger.LogInformation("Teams notification Graph Service Client initialized successfully");
                return _graphServiceClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Teams notification Graph Service Client");
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
                var teamId = _configuration["TEAMS_TEAM_ID"];
                var channelId = _configuration["TEAMS_CHANNEL_ID"];

                if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(channelId))
                {
                    _logger.LogError("Teams Team ID or Channel ID is not configured");
                    return false;
                }

                var messageContent = CreateAdaptiveCardMessage(enrollmentEvent);
                return await SendTeamsMessageAsync(teamId, channelId, messageContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Teams notification for device {DeviceName}", enrollmentEvent.DeviceName);
                return false;
            }
        }

        private async Task<bool> SendTeamsMessageAsync(string teamId, string channelId, ChatMessage message)
        {
            try
            {
                var graphClient = await GetGraphServiceClientAsync();
                
                await graphClient.Teams[teamId]
                    .Channels[channelId]
                    .Messages
                    .Request()
                    .AddAsync(message);

                _logger.LogInformation("Teams message sent successfully to channel {ChannelId}", channelId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Teams message. TeamId: {TeamId}, ChannelId: {ChannelId}", teamId, channelId);
                return false;
            }
        }

        private ChatMessage CreateAdaptiveCardMessage(EnrollmentEvent enrollmentEvent)
        {
            var statusIcon = GetStatusIcon(enrollmentEvent.EventType);
            var statusColor = GetStatusColor(enrollmentEvent.EventType);
            
            // Create Adaptive Card JSON
            var adaptiveCard = new
            {
                type = "AdaptiveCard",
                body = new object[]
                {
                    new
                    {
                        type = "Container",
                        style = "emphasis",
                        items = new object[]
                        {
                            new
                            {
                                type = "ColumnSet",
                                columns = new object[]
                                {
                                    new
                                    {
                                        type = "Column",
                                        width = "auto",
                                        items = new object[]
                                        {
                                            new
                                            {
                                                type = "TextBlock",
                                                text = statusIcon,
                                                size = "ExtraLarge",
                                                wrap = true
                                            }
                                        }
                                    },
                                    new
                                    {
                                        type = "Column",
                                        width = "stretch",
                                        items = new object[]
                                        {
                                            new
                                            {
                                                type = "TextBlock",
                                                text = $"Device Enrollment {enrollmentEvent.EventType.ToUpper()}",
                                                weight = "Bolder",
                                                size = "Large",
                                                wrap = true
                                            },
                                            new
                                            {
                                                type = "TextBlock",
                                                text = enrollmentEvent.DeviceName,
                                                weight = "Bolder",
                                                size = "Medium",
                                                color = statusColor,
                                                wrap = true
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new
                    {
                        type = "FactSet",
                        facts = new object[]
                        {
                            new { title = "Status", value = enrollmentEvent.EventType },
                            new { title = "Device Name", value = enrollmentEvent.DeviceName },
                            new { title = "User", value = $"{enrollmentEvent.UserDisplayName} ({enrollmentEvent.UserPrincipalName})" },
                            new { title = "Platform", value = $"{enrollmentEvent.OperatingSystem} {enrollmentEvent.OsVersion}" },
                            new { title = "Timestamp", value = enrollmentEvent.ProcessedDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" }
                        }
                    },
                    new
                    {
                        type = "Container",
                        items = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = "Diagnostic Information",
                                weight = "Bolder",
                                size = "Medium",
                                wrap = true,
                                isVisible = !string.IsNullOrEmpty(enrollmentEvent.DiagnosticInfo)
                            },
                            new
                            {
                                type = "TextBlock",
                                text = enrollmentEvent.DiagnosticInfo ?? "",
                                wrap = true,
                                isVisible = !string.IsNullOrEmpty(enrollmentEvent.DiagnosticInfo)
                            }
                        }
                    },
                    new
                    {
                        type = "Container",
                        items = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = "Troubleshooting Steps",
                                weight = "Bolder",
                                size = "Medium",
                                wrap = true,
                                isVisible = !string.IsNullOrEmpty(enrollmentEvent.TroubleshootingSteps)
                            },
                            new
                            {
                                type = "TextBlock",
                                text = enrollmentEvent.TroubleshootingSteps ?? "",
                                wrap = true,
                                isVisible = !string.IsNullOrEmpty(enrollmentEvent.TroubleshootingSteps)
                            }
                        }
                    }
                },
                schema = "http://adaptivecards.io/schemas/adaptive-card.json",
                version = "1.4"
            };

            var attachmentContent = Newtonsoft.Json.JsonConvert.SerializeObject(adaptiveCard);

            var message = new ChatMessage
            {
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = $"<attachment id=\"{Guid.NewGuid()}\"></attachment>"
                },
                Attachments = new List<ChatMessageAttachment>
                {
                    new ChatMessageAttachment
                    {
                        Id = Guid.NewGuid().ToString(),
                        ContentType = "application/vnd.microsoft.card.adaptive",
                        ContentUrl = null,
                        Content = attachmentContent,
                        Name = null,
                        ThumbnailUrl = null
                    }
                }
            };

            return message;
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

        private string GetStatusColor(string eventType)
        {
            return eventType switch
            {
                "Success" => "Good",
                "Failure" => "Attention",
                "Warning" => "Warning",
                _ => "Default"
            };
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var teamId = _configuration["TEAMS_TEAM_ID"];
                var channelId = _configuration["TEAMS_CHANNEL_ID"];

                if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(channelId))
                {
                    _logger.LogError("Teams Team ID or Channel ID is not configured");
                    return false;
                }

                var graphClient = await GetGraphServiceClientAsync();
                
                // Test by getting the channel info
                var channel = await graphClient.Teams[teamId]
                    .Channels[channelId]
                    .Request()
                    .GetAsync();

                _logger.LogInformation("Teams connection test successful. Channel: {ChannelName}", channel.DisplayName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Teams connection test failed");
                return false;
            }
        }
    }
}
