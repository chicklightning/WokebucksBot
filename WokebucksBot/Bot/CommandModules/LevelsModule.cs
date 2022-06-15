using Discord;
using Discord.Commands;
using Swamp.WokebucksBot.Bot.Extensions;
using Swamp.WokebucksBot.CosmosDB;

namespace Swamp.WokebucksBot.Bot.CommandModules
{
    public class LevelsModule : ModuleBase<SocketCommandContext>
    {
		private const string CommandName = "CommandName";
		private const string UserIdKey = "UserId";

		private readonly ILogger<LevelsModule> _logger;
		private readonly CosmosDBClient _documentClient;

		public LevelsModule(ILogger<LevelsModule> logger, CosmosDBClient docClient)
		{
			_logger = logger;
			_documentClient = docClient;
		}

		[Command("level")]
		[Summary("Provides information on the your current level.")]
		public async Task GetLevelAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "level", Context.User.GetFullUsername());

			UserData? user = await _documentClient.GetDocumentAsync<UserData>(Context.User.Id.ToString());
			if (user is null)
			{
				user = new UserData(Context.User);
				await _documentClient.UpsertDocumentAsync<UserData>(user);
			}

			string description = (user.Level > 0) ? $"You are currently a **{Levels.AllLevels[user.Level].Name}**." : "You have not purchased any levels.";
			var embedBuilder = new EmbedBuilder()
                                        .WithColor((user.Level > 0) ? Levels.AllLevels[user.Level].Color : Color.Teal)
										.WithTitle($"{Context.User.GetFullUsername()}'s Level")
										.WithDescription(description)
										.WithFooter($"{Context.User.GetFullUsername()}'s Level Inquiry handled by Wokebucks")
										.WithUrl("https://github.com/chicklightning/WokebucksBot")
										.WithCurrentTimestamp();

			await ReplyAsync("", embed: embedBuilder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "level", Context.User.GetFullUsername());
		}

		[Command("levels")]
		[Summary("Provides information on all levels available.")]
		public async Task GetLevelsAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "levels", Context.User.GetFullUsername());

			var embedBuilder = new EmbedBuilder()
										.WithColor(Color.Teal)
										.WithTitle("Wokebucks Levels")
										.WithDescription("Provides the name of each level, the amount required to purchase the level, and limit increases for giving and receiving Wokebucks.")
										.WithFooter($"{Context.User.GetFullUsername()}'s Levels Inquiry handled by Wokebucks")
										.WithUrl("https://github.com/chicklightning/WokebucksBot")
										.WithCurrentTimestamp();

			foreach (var level in Levels.AllLevels.Values)
            {
				embedBuilder.AddField(level.Name, $"Costs ${string.Format("{0:0.00}", level.Amount)} to purchase. Can give up to ${string.Format("{0:0.00}", level.UpperLimit)} and take up to ${string.Format("{0:0.00}", level.LowerLimit)}.");
            }

			await ReplyAsync("", embed: embedBuilder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "levels", Context.User.GetFullUsername());
		}

		[Command("buylevel")]
		[Summary("Provides information on the your current level and the next level in the tier.")]
		public async Task BuyLevelAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "buylevel", Context.User.GetFullUsername());

			UserData? user = await _documentClient.GetDocumentAsync<UserData>(Context.User.Id.ToString());
			if (user is null)
			{
				user = new UserData(Context.User);
				await _documentClient.UpsertDocumentAsync<UserData>(user);
			}

			string description = (user.Level > 0) ? $"You are currently a **{Levels.AllLevels[user.Level].Name}**." : "You have not purchased any levels.";
			var embedBuilder = new EmbedBuilder()
										.WithColor((user.Level > 0) ? Levels.AllLevels[user.Level].Color : Color.Teal)
										.WithTitle($"{Context.User.GetFullUsername()}'s Level")
										.WithDescription(description)
										.WithFooter($"{Context.User.GetFullUsername()}'s Level Purchase Inquiry handled by Wokebucks")
										.WithUrl("https://github.com/chicklightning/WokebucksBot")
										.WithCurrentTimestamp();

			if (user.Level < 11)
			{
				embedBuilder.AddField("Next Level", $"{Levels.AllLevels[user.Level + 1].Name}");
				embedBuilder.AddField("Cost to Purchase", "$" + string.Format("{0:0.00}", Levels.AllLevels[user.Level + 1].Amount));
				embedBuilder.AddField("Your Balance", "$" + string.Format("{0:0.00}", user.Balance));

				var flyingDollarEmoji = new Emoji("\uD83D\uDCB8");
				var buttonBuilder = new ButtonBuilder()
											.WithEmote(flyingDollarEmoji)
											.WithLabel("Agree to purchase?")
											.WithCustomId("level")
											.WithStyle(ButtonStyle.Success);

				var componentBuilder = new ComponentBuilder()
											.WithButton(buttonBuilder);

				await ReplyAsync("", embed: embedBuilder.Build(), components: componentBuilder.Build());
			}
			else // User is at the highest level already, they can't buy any more levels
			{
				embedBuilder.AddField("Next Level", "You have achieved the highest level.");
				await ReplyAsync("", embed: embedBuilder.Build());
			}

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "buylevel", Context.User.GetFullUsername());
		}
	}
}
