using Discord.Commands;

namespace Swamp.WokebucksBot.Discord
{
    public class DiscordBotWorker : BackgroundService
    {
        private readonly ILogger<DiscordBotWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly DiscordClient _discordClient;

        public DiscordBotWorker(ILogger<DiscordBotWorker> logger, IConfiguration configuration, DiscordClient discordClient)
        {
            _logger = logger;
            _configuration = configuration;
            _discordClient = discordClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running.");

                try
                {
                    await _discordClient.InitializeAsync(_configuration["DiscordToken"]);
                    await Task.Delay(-1, stoppingToken);
                }
                catch (Exception e)
                {
                    if (e is not TaskCanceledException)
                    {
                        _logger.LogError(e, "Issue initializing DiscordClient.");
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
    }
}