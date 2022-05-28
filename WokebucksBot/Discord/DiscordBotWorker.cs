using Discord.Commands;

namespace Swamp.WokebucksBot.Discord
{
    public class DiscordBotWorker : BackgroundService
    {
        private readonly ILogger<DiscordBotWorker> _logger;
        private readonly DiscordClient _discordClient;

        public DiscordBotWorker(ILogger<DiscordBotWorker> logger, DiscordClient discordClient)
        {
            _logger = logger;
            _discordClient = discordClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running.");

                await _commandHandler.InitializeAsync(Environment.GetEnvironmentVariable("DiscordToken"));
                await _discordClient.StartAsync();

                await Task.Delay(-1, stoppingToken);
            }
        }
    }
}