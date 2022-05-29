using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;
using Swamp.WokebucksBot.CosmosDB;
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
                    string? appConfigEndpoint = Environment.GetEnvironmentVariable("AppConfigEndpoint");
                    if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
                    {
                        var credentials = new DefaultAzureCredential();
                        builder.AddAzureAppConfiguration(options => options.Connect(new Uri(appConfigEndpoint), credentials)
                                                                           .ConfigureKeyVault(kv =>
                                                                           {
                                                                               kv.SetCredential(credentials);
                                                                               kv.SetSecretResolver(identifier =>
                                                                               {
                                                                                   string? secretValue = null;
                                                                                   try
                                                                                   {
                                                                                       var secretName = identifier?.Segments?.ElementAtOrDefault(2)?.TrimEnd('/');
                                                                                       var secretVersion = identifier?.Segments?.ElementAtOrDefault(3)?.TrimEnd('/');
                                                                                       if (identifier is not null)
                                                                                       {
                                                                                           var secretClient = new SecretClient(new Uri(identifier.GetLeftPart(UriPartial.Authority)),
                                                                                               credentials);

                                                                                           KeyVaultSecret secret = secretClient.GetSecret(secretName, secretVersion);
                                                                                           secretValue = secret?.Value;
                                                                                       }
                                                                                   }
                                                                                   catch (UnauthorizedAccessException)
                                                                                   {
                                                                                       secretValue = string.Empty;
                                                                                   }

                                                                                   return new ValueTask<string>(secretValue);
                                                                               });
                                                                           }));
                    }
                })
                .ConfigureServices(services =>
                {
                    // The following line enables Application Insights telemetry collection.
                    services.AddApplicationInsightsTelemetryWorkerService(instrumentationKey: Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY"));

                    services.AddSingleton<CosmosDBClient>(serviceProvider =>
                    {
                        ILogger<CosmosDBClient> logger = serviceProvider.GetRequiredService<ILogger<CosmosDBClient>>();
                        IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();

                        if (string.Equals(configuration["DOTNET_ENVIRONMENT"], "Development"))
                        {
                            return new CosmosDBClient(logger, configuration["CosmosDBConnector"]);
                        }
                        else
                        {
                            return new CosmosDBClient(logger, configuration["CosmosDBConnector"], new DefaultAzureCredential());
                        }
                    });
                    services.AddSingleton<DiscordSocketClient>(serviceProvider =>
                    {
                        var config = new DiscordSocketConfig()
                        {
                            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions,
                            AlwaysDownloadUsers = true,
                        };

                        return new DiscordSocketClient(config);
                    });
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