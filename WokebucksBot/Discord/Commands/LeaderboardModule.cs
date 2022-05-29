using Discord;
using Discord.Commands;
using Swamp.WokebucksBot.CosmosDB;

namespace Swamp.WokebucksBot.Discord.Commands
{
    public class LeaderboardModule : ModuleBase<SocketCommandContext>
	{
		private const string CommandName = "CommandName";
		private const string UserIdKey = "UserId";

		private readonly ILogger<LeaderboardModule> _logger;
		private readonly CosmosDBClient _documentClient;

		public LeaderboardModule(ILogger<LeaderboardModule> logger, CosmosDBClient docClient)
		{
			_logger = logger;
			_documentClient = docClient;
		}

		[Command("leaderboard")]
		[Summary("Who is the most woke?")]
		public async Task FetchLeaderboardAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "leaderboard", Context.User.GetFullUsername());

			Leaderboard? leaderboard = await _documentClient.GetDocumentAsync<Leaderboard>("leaderboard");

			if (leaderboard is null)
			{
				var e = new InvalidOperationException("Could not find leaderboard.");
				_logger.LogError(e, $"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}>.", "givebucks", Context.User.GetFullUsername());
				throw e;
			}

			var embedBuilder = new EmbedBuilder();
			embedBuilder.WithColor(Color.Gold);
			embedBuilder.WithTitle("Leaderboard");
			foreach (var leaderboardKeyValuePair in leaderboard.TopThreeWokest)
			{
				embedBuilder.AddField($"{leaderboardKeyValuePair.Key}", $"${leaderboardKeyValuePair.Value}");
			}
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Leaderboard Request handled by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

			await ReplyAsync($"", false, embed: embedBuilder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "leaderboard", Context.User.GetFullUsername());
		}

		[Command("skeeterboard")]
		[Summary("Who is the most problematic?")]
		public async Task FetchSkeeterboardAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "skeeterboard", Context.User.GetFullUsername());

			Leaderboard? leaderboard = await _documentClient.GetDocumentAsync<Leaderboard>("leaderboard");

			if (leaderboard is null)
			{
				var e = new InvalidOperationException("Could not find leaderboard.");
				_logger.LogError(e, $"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}>.", "givebucks", Context.User.GetFullUsername());
				throw e;
			}

			var embedBuilder = new EmbedBuilder();
			embedBuilder.WithColor(Color.Gold);
			embedBuilder.WithTitle("Skeeterboard");
			foreach (var leaderboardKeyValuePair in leaderboard.BottomThreeWokest)
			{
				embedBuilder.AddField($"{leaderboardKeyValuePair.Key}", $"${leaderboardKeyValuePair.Value}");
			}
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Skeeterboard Request handled by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

			await ReplyAsync($"", false, embed: embedBuilder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "skeeterboard", Context.User.GetFullUsername());
		}

		[Command("amiwoke")]
		[Summary("Are you woke?")]
		[Alias("woke", "help")]
		public async Task AmIWokeAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "amiwoke", Context.User.GetFullUsername());

			Leaderboard? leaderboard = await _documentClient.GetDocumentAsync<Leaderboard>("leaderboard");

			if (leaderboard is null)
			{
				var e = new InvalidOperationException("Could not find leaderboard.");
				_logger.LogError(e, $"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}>.", "givebucks", Context.User.GetFullUsername());
				throw e;
			}

			var embedBuilder = new EmbedBuilder();
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Wokeness provided by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
			if (leaderboard.TopThreeWokest.ContainsKey(Context.User.GetFullUsername()))
            {
				embedBuilder.WithColor(Color.Green);
				embedBuilder.WithTitle("You are **woke**.");
			}
			else if (leaderboard.BottomThreeWokest.ContainsKey(Context.User.GetFullUsername()))
            {
				embedBuilder.WithColor(Color.Red);
				embedBuilder.WithTitle("You are **problematic**.");

				embedBuilder.WithImageUrl("https://c.tenor.com/menhdadhvLgAAAAC/problematic-bo.gif");
			}
			else
			{
				embedBuilder.WithColor(Color.Green);
				embedBuilder.WithTitle("You are **not woke**.");
			}

			await ReplyAsync($"", false, embed: embedBuilder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "amiwoke", Context.User.GetFullUsername());
		}
	}
}
