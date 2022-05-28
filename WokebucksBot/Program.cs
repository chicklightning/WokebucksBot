using Azure.Identity;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;
using Swamp.WokebucksBot.Discord;

namespace Swamp.WokebucksBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    string appConfigEndpoint = Environment.GetEnvironmentVariable("AppConfigEndpoint");
                    if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
                    {
                        builder.AddAzureAppConfiguration(options => options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential()));
                    }
                })
                .ConfigureServices(services =>
                {
                    // The following line enables Application Insights telemetry collection.
                    services.AddApplicationInsightsTelemetryWorkerService(instrumentationKey: Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING"));

                    services.AddSingleton<DiscordSocketClient>();
                    services.AddSingleton<CommandService>();
                    services.AddSingleton<DiscordClient>();
                    services.AddHostedService<DiscordBotWorker>();

                })
                .UseSerilog((_, serviceProvider, loggerConfig) =>
                    loggerConfig
                        .WriteTo.Console(Serilog.Events.LogEventLevel.Verbose)
                        .WriteTo.ApplicationInsights(serviceProvider.GetRequiredService<TelemetryConfiguration>(), TelemetryConverter.Traces))
                .Build();

            host.Run();
        }
    }
}