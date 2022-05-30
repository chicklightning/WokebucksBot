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

		[Command("givebucks")]
		[Summary("Adds a specified amount of Wokebucks to another user's Wokebucks balance (allowed once per five minutes per unique user).")]
		[Alias("give", "add")]
		public async Task GiveWokebuckAsync(
			[Summary("The amount of Wokebucks you want to add, between 1 and 10.")]
			double amount,
			[Summary("The user whose balance you want to add Wokebucks to.")]
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

			await CheckUserInteractionsAndUpdateBalances(Context, user, "givebuck", amount);
		}

		[Command("takebucks")]
		[Summary("Takes a specified amount of Wokebucks from another user's Wokebucks balance (allowed once per five minutes per unique user).")]
		[Alias("take")]
		public async Task TakeWokebuckAsync(
			[Summary("The amount of Wokebucks you want to take, between 1 and 5.")]
			double amount,
			[Summary("The user whose balance you want to take Wokebucks from.")]
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

			await CheckUserInteractionsAndUpdateBalances(Context, user, "takebuck", amount);
		}

		[Command("balance")]
		[Summary("Checks your balance.")]
		[Alias("check", "checkbucks")]
		public async Task CheckWokebucksAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "balance", Context.User.GetFullUsername());

			UserData? userData = await _documentClient.GetDocumentAsync<UserData>(Context.User.GetFullDatabaseId());

			if (userData is null)
			{
				userData = new UserData(Context.User.GetFullDatabaseId());
				await _documentClient.UpsertDocumentAsync<UserData>(userData);
			}

			var embedBuilder = new EmbedBuilder();
			embedBuilder.WithColor((userData.IsOverdrawn() ? Color.Red : Color.Green));
			embedBuilder.WithTitle("$" + string.Format("{0:0.00}", userData.Balance));
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Balance provided by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

			await ReplyAsync($"", false, embed: embedBuilder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "balance", Context.User.GetFullUsername());
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
				embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

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

		private async Task CheckUserInteractionsAndUpdateBalances(SocketCommandContext context, SocketUser target, string commandName, double amount)
        {
			SocketUser caller = context.User;

			// Check user's relationship to other user to make sure at least an hour has passed
			Task<UserData?> callerDataFetchTask = _documentClient.GetDocumentAsync<UserData>(caller.GetFullDatabaseId());
			Task<Leaderboard?> leaderboardFetchTask = _documentClient.GetDocumentAsync<Leaderboard>("leaderboard");

			await Task.WhenAll(callerDataFetchTask, leaderboardFetchTask);

			UserData callerData = await callerDataFetchTask ?? new UserData(caller.GetFullDatabaseId());
			Leaderboard? leaderboard = await leaderboardFetchTask;

			if (leaderboard is null)
			{
				var e = new InvalidOperationException("Could not find leaderboard.");
				_logger.LogError(e, "Could not find leaderboard.");
				throw e;
			}

			var embedBuilder = new EmbedBuilder();

			// Bot owner can call commands unlimited times
			IApplication application = await context.Client.GetApplicationInfoAsync().ConfigureAwait(continueOnCapturedContext: false);
			double minutesSinceLastInteractionWithOtherUser = context.User.Id != application.Owner.Id ? callerData.GetMinutesSinceLastUserInteractionTime(target.GetFullDatabaseId()) : double.MaxValue;
			if (minutesSinceLastInteractionWithOtherUser < 5)
			{
				// If 5 minutes has not passed, send message saying they have not waited at least 5 min since their last Wokebuck gift, and that x minutes are remaining
				embedBuilder.WithColor(Color.Red);
				embedBuilder.WithTitle("Invalid Bank Transaction");
				embedBuilder.WithDescription($"Sorry, you have to wait at least **{5 - (int)minutesSinceLastInteractionWithOtherUser} minutes** before you can give Wokebucks to or remove Wokebucks from **{target.GetFullUsername()}**'s balance.");
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Message provided by Wokebucks");
				embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

				await ReplyAsync($"", false, embed: embedBuilder.Build());

				_logger.LogInformation($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting user <{{{TargetUserIdKey}}}>.", commandName, Context.User.GetFullDatabaseId(), target.GetFullUsername());
			}
			else // minutesSinceLastInteractionWithOtherUser >= 5
			{
				// Let the bot owner do whatever amount, others can only subtract at most 5 or add at most 10
				UserData targetData = await _documentClient.GetDocumentAsync<UserData>(target.GetFullDatabaseId()) ?? new UserData(target.GetFullDatabaseId());

				if (context.User.Id != application.Owner.Id && (amount > 10 || amount < -5))
                {
					// If amount needs to be within bounds for normal users
					embedBuilder.WithColor(Color.Red);
					embedBuilder.WithTitle("Invalid Bank Transaction");
					embedBuilder.WithDescription($"Sorry, you have select a value larger than $-5.00 or smaller than $10.00.");
					embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Message provided by Wokebucks");
					embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

					await ReplyAsync($"", false, embed: embedBuilder.Build());

					_logger.LogInformation($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting user <{{{TargetUserIdKey}}}>: Wokebucks value invalid.", commandName, Context.User.GetFullDatabaseId(), target.GetFullUsername());
				}

				targetData.AddToBalance(amount);

				// Update leaderboard
				leaderboard.UpdateLeaderboard(target.GetFullUsername(), targetData.Balance);

				callerData.UpdateMostRecentInteractionForUser(target.GetFullDatabaseId());
				Task updateTargetDataTask = _documentClient.UpsertDocumentAsync<UserData>(targetData);
				Task updateCallerDataTask = _documentClient.UpsertDocumentAsync<UserData>(callerData);
				Task updateLeaderboard = _documentClient.UpsertDocumentAsync<Leaderboard>(leaderboard);

				await Task.WhenAll(updateTargetDataTask, updateCallerDataTask, updateLeaderboard);

				embedBuilder.WithColor((targetData.IsOverdrawn() ? Color.Red : Color.Green));
				embedBuilder.WithTitle("Bank Transaction");
				embedBuilder.AddField($"{(commandName.Contains("take") ? "Victim" : "Recipient")}", $"{target.GetFullUsername()}", true);
				embedBuilder.AddField("Updated Balance", "$" + string.Format("{0:0.00}", targetData.Balance), true);
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Transaction provided by Wokebucks");
				embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

				await ReplyAsync($"", false, embed: embedBuilder.Build());

				_logger.LogInformation($"<{{{CommandName}}}> command successfully completed by user <{{{UserIdKey}}}> for user <{{{TargetUserIdKey}}}> with updated balance <{targetData.Balance}>.", commandName, Context.User.GetFullDatabaseId(), targetData.ID);
			}
		}
	}
}
