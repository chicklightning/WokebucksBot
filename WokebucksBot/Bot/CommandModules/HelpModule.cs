using Discord;
using Discord.Commands;
using Swamp.WokebucksBot.Bot.Extensions;

namespace Swamp.WokebucksBot.Bot.CommandModules
{
    public  class HelpModule : ModuleBase<SocketCommandContext>
    {
        private const string CommandName = "CommandName";
        private const string UserIdKey = "UserId";

        private readonly ILogger<HelpModule> _logger;
        private readonly CommandService _commandService;

        public HelpModule(ILogger<HelpModule> logger, CommandService commandService)
        {
            _logger = logger;
            _commandService = commandService;
        }

        [Command("info")]
        [Summary("Provides information on other commands and how they're used and called.")]
        public async Task CommandInfo()
        {
            _logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "info", Context.User.GetFullUsername());

            List<CommandInfo> commands = _commandService.Commands.ToList();

            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithTitle("Wokebucks Bot Commands");
            embedBuilder.WithColor(Color.Blue);
            embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Info Request resolved by Wokebucks");
            embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
            embedBuilder.WithCurrentTimestamp();
            foreach (CommandInfo command in commands)
            {
                // Get the command Summary attribute information
                string embedFieldText = command.Summary ?? "No description available\n";

                embedBuilder.AddField($"${command.Name}", embedFieldText);
            }

            await ReplyAsync("", false, embedBuilder.Build());
            _logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "info", Context.User.GetFullUsername());
        }
    }
}
