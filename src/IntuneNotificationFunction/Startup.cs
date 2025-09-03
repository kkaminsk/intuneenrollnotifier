using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.ApplicationInsights.Extensibility;
using IntuneNotificationFunction.Services;
using Microsoft.ApplicationInsights;

[assembly: FunctionsStartup(typeof(IntuneNotificationFunction.Startup))]

namespace IntuneNotificationFunction
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Register services
            builder.Services.AddSingleton<GraphService>();
            builder.Services.AddSingleton<EmailService>();
            builder.Services.AddSingleton<TeamsNotificationService>();
            
            // Register Application Insights
            builder.Services.AddApplicationInsightsTelemetry();
            builder.Services.AddSingleton<TelemetryClient>();
            
            // Configure logging
            builder.Services.AddLogging();
        }
    }
}
