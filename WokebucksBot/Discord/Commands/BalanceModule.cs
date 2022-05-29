using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Swamp.WokebucksBot.CosmosDB;

namespace Swamp.WokebucksBot.Discord.Commands
{
    public class BalanceModule : ModuleBase<SocketCommandContext>
    {
		private const string CommandName = "CommandName";
		private const string UserIdKey = "UserId";
		private const string TargetUserIdKey = "TargetUserId";

		private readonly ILogger<BalanceModule> _logger;
		private readonly CosmosDBClient _documentClient;

		public BalanceModule(ILogger<BalanceModule> logger, CosmosDBClient docClient)
        {
			_logger = logger;
			_documentClient = docClient;
        }

		[Command("givebuck")]
		[Summary("Adds $1 to another user's Wokebucks balance (allowed once per hour per unique user).")]
		[Alias("give", "add")]
		public async Task GiveWokebuckAsync(
			[Summary("The user whose balance you want to add a Wokebuck to.")]
			SocketUser? target = null)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "givebuck", Context.User.GetFullUsername());

			SocketUser? user = target ?? Context.Message.MentionedUsers.FirstOrDefault();
			if (user is null)
            {
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting unknown user.", "givebuck", Context.User.GetFullUsername());
				return;
            }

			if (await ReactIfSelfWhereNotAllowedAsync(Context.User, user, Context.Message))
			{
				return;
			}

			await CheckUserInteractionsAndUpdateBalances(Context.User, user, "givebuck");
		}

		[Command("givebucks")]
		[Summary("Adds $10 to another user's Wokebucks balance (allowed once per two hours per unique user).")]
		[Alias("givebig", "addmuch")]
		public async Task GiveWokebucksAsync(
			[Summary("The user whose balance you want to add Wokebucks to.")]
			SocketUser? target = null)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "givebucks", Context.User.GetFullUsername());

			SocketUser? user = target ?? Context.Message.MentionedUsers.FirstOrDefault();
			if (user is null)
			{
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting unknown user.", "givebucks", Context.User.GetFullUsername());
				return;
			}

			if (await ReactIfSelfWhereNotAllowedAsync(Context.User, user, Context.Message))
			{
				return;
			}

			await CheckUserInteractionsAndUpdateBalances(Context.User, user, "givebucks");
		}

		[Command("takebuck")]
		[Summary("Takes $1 from another user's Wokebucks balance (allowed once per hour per unique user).")]
		[Alias("take")]
		public async Task TakeWokebuckAsync(
			[Summary("The user whose balance you want to take a Wokebuck from.")]
			SocketUser? target = null)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "takebuck", Context.User.GetFullUsername());

			SocketUser? user = target ?? Context.Message.MentionedUsers.FirstOrDefault();
			if (user is null)
			{
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting unknown user.", "takebuck", Context.User.GetFullUsername());
				return;
			}

			if (await ReactIfSelfWhereNotAllowedAsync(Context.User, user, Context.Message))
			{
				return;
			}

			await CheckUserInteractionsAndUpdateBalances(Context.User, user, "takebuck");
		}

		[RequireOwner]
		[Command("takebucks")]
		[Summary("Takes $10 from another user's Wokebucks balance (allowed once per two hours per unique user).")]
		[Alias("takebig")]
		public async Task TakeWokebucksAsync(
			[Summary("The user whose balance you want to take Wokebucks from.")]
			SocketUser? target = null)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "takebucks", Context.User.GetFullUsername());

			SocketUser? user = target ?? Context.Message.MentionedUsers.FirstOrDefault();
			if (user is null)
			{
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting unknown user.", "takebucks", Context.User.GetFullUsername());
				return;
			}

			if (await ReactIfSelfWhereNotAllowedAsync(Context.User, user, Context.Message))
            {
				return;
            }

			await CheckUserInteractionsAndUpdateBalances(Context.User, user, "takebucks");
		}

		[Command("balance")]
		[Summary("Checks your balance.")]
		[Alias("check", "checkbucks")]
		public async Task CheckWokebucksAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "balance", Context.User.GetFullUsername());

			UserData? userData = await _documentClient.GetUserDataAsync(Context.User.GetFullDatabaseId());

			if (userData is null)
			{
				userData = new UserData(Context.User.GetFullDatabaseId());
				await _documentClient.UpsertUserDataAsync(userData);
			}

			var embedBuilder = new EmbedBuilder();
			embedBuilder.WithColor((userData.IsOverdrawn() ? Color.Red : Color.Green));
			embedBuilder.WithTitle($"${userData.Balance}");
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Balance provided by Wokebucks");

			await ReplyAsync($"", false, embed: embedBuilder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "balance", Context.User.GetFullUsername());
		}

		[Command("amiwoke")]
		[Summary("Are you woke?")]
		[Alias("woke", "help")]
		public async Task AmIWokeAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "amiwoke", Context.User.GetFullUsername());

			UserData? userData = await _documentClient.GetUserDataAsync(Context.User.GetFullDatabaseId());

			if (userData is null)
			{
				userData = new UserData(Context.User.GetFullDatabaseId());
				await _documentClient.UpsertUserDataAsync(userData);
			}

			var embedBuilder = new EmbedBuilder();
			embedBuilder.WithColor((userData.IsOverdrawn() ? Color.Red : Color.Green));
			embedBuilder.WithTitle($"You are {(userData.IsOverdrawn() ? "**not** " : string.Empty)}**woke**.");
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Wokeness provided by Wokebucks");

			await ReplyAsync($"", false, embed: embedBuilder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "amiwoke", Context.User.GetFullUsername());
		}

		private async Task<bool> ReactIfSelfWhereNotAllowedAsync(SocketUser caller, SocketUser target, SocketUserMessage userMessage)
        {
			if (string.Equals(caller.GetFullUsername(), target.GetFullUsername()))
			{
				var embedBuilder = new EmbedBuilder();
				embedBuilder.WithColor(Color.Red);
				embedBuilder.WithTitle("Invalid Bank Transaction");
				embedBuilder.WithDescription("You can't change your own Wokebucks ~~dumbass~~.");
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Message provided by Wokebucks");

				await ReplyAsync($"", false, embed: embedBuilder.Build());

				var emote = Context.Guild.Emotes
							.FirstOrDefault(x => x.Name.IndexOf(
								"OofaDoofa", StringComparison.OrdinalIgnoreCase) != -1);
				if (emote is not null)
				{
					await userMessage.AddReactionAsync(emote);
				}

				return true;
			}

			return false;
		}

		private async Task CheckUserInteractionsAndUpdateBalances(SocketUser caller, SocketUser target, string commandName)
        {
			// Check user's relationship to other user to make sure at least an hour has passed
			UserData? callerData = await _documentClient.GetUserDataAsync(caller.GetFullDatabaseId()) ?? new UserData(caller.GetFullDatabaseId());
			double minutesSinceLastInteractionWithOtherUser = callerData.GetMinutesSinceLastUserInteractionTime(target.GetFullDatabaseId());
			if (minutesSinceLastInteractionWithOtherUser < 60)
			{
				// If an hour has not passed, send message saying they have not waited an hour since their last Wokebuck gift, and that x minutes are remaining
				var embedBuilder = new EmbedBuilder();
				embedBuilder.WithColor(Color.Red);
				embedBuilder.WithTitle("Invalid Bank Transaction");
				embedBuilder.WithDescription($"Sorry, you have to wait at least **{60 - (int)minutesSinceLastInteractionWithOtherUser} minutes** before you can give Wokebucks to or remove Wokebucks from **{target.GetFullUsername()}**'s balance.");
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Message provided by Wokebucks");

				await ReplyAsync($"", false, embed: embedBuilder.Build());

				_logger.LogInformation($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting user <{{{TargetUserIdKey}}}>.", commandName, Context.User.GetFullDatabaseId(), target.GetFullUsername());
			}
			else // minutesSinceLastInteractionWithOtherUser >= 60
			{
				// If hour has passed, allow user to give other user a Wokebuck, send an updated balance for the other user, and update most recent interaction for the caller
				UserData targetData = await _documentClient.GetUserDataAsync(target.GetFullDatabaseId()) ?? new UserData(target.GetFullDatabaseId());
				
				switch (commandName)
                {
					case "givebuck":
					{
						targetData.AddDollarToBalance();
						break;
					}
					case "givebucks":
                    {
						targetData.AddTenDollarsToBalance();
						break;
                    }
					case "takebuck":
                    {
						targetData.SubtractDollarFromBalance();
						break;
                    }
					case "takebucks":
                    {
						targetData.SubtractTenDollarsFromBalance();
						break;
                    }
					default:
                    {
						break;
                    }
                }

				callerData.UpdateMostRecentInteractionForUser(target.GetFullDatabaseId());
				Task updateTargetDataTask = _documentClient.UpsertUserDataAsync(targetData);
				Task updateCallerDataTask = _documentClient.UpsertUserDataAsync(callerData);

				await Task.WhenAll(updateTargetDataTask, updateCallerDataTask);

				var embedBuilder = new EmbedBuilder();
				embedBuilder.WithColor((targetData.IsOverdrawn() ? Color.Red : Color.Green));
				embedBuilder.WithTitle("Bank Transaction");
				embedBuilder.AddField($"{(commandName.Contains("take") ? "Victim" : "Recipient")}", $"{target.GetFullUsername()}", true);
				embedBuilder.AddField("Updated Balance", $"${targetData.Balance}", true);
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Transaction provided by Wokebucks");

				await ReplyAsync($"", false, embed: embedBuilder.Build());

				_logger.LogInformation($"<{{{CommandName}}}> command successfully completed by user <{{{UserIdKey}}}> for user <{{{TargetUserIdKey}}}> with updated balance <{targetData.Balance}>.", commandName, Context.User.GetFullDatabaseId(), targetData.ID);
			}
		}
	}
}
