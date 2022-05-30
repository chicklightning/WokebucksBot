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
		private readonly DiscordSocketClient _discordSocketClient;

		public BalanceModule(ILogger<BalanceModule> logger, DiscordSocketClient discordSocketClient, CosmosDBClient docClient)
        {
			_logger = logger;
			_discordSocketClient = discordSocketClient;
			_documentClient = docClient;
        }

		[Command("givebucks")]
		[Summary("Adds a specified amount of Wokebucks to another user's Wokebucks balance (allowed once per five minutes per unique user).")]
		public async Task GiveWokebuckAsync(
			[Summary("The amount of Wokebucks you want to add, between 0.01 and 10.")]
			double amount,
			[Summary("The reason you are giving them Wokebucks.")]
			string reason,
			[Summary("The user whose balance you want to add Wokebucks to.")]
			SocketUser? target = null)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "givebucks", Context.User.GetFullUsername());

			SocketUser? user = target ?? Context.Message.MentionedUsers.FirstOrDefault();
			var embedBuilder = new EmbedBuilder();
			if (user is null)
            {
				await RespondWithFormattedError(embedBuilder, $"You have to mention a user in order to add to their balance.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting unknown user.", "givebucks", Context.User.GetFullUsername());
				return;
            }

			IApplication application = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(continueOnCapturedContext: false);
			if (double.IsNaN(amount) || amount < 0.01 || (amount > 10 && application.Owner.Id != Context.User.Id))
			{
				await RespondWithFormattedError(embedBuilder, $"You must select an amount between $0.01 and $10.00.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> attempting to assign an invalid value: <{amount}>.", "givebucks", Context.User.GetFullUsername());
				return;
			}

			if (await ReactIfSelfWhereNotAllowedAsync(application, user, Context.Message))
			{
				return;
			}

			var filter = new ProfanityFilter.ProfanityFilter();
			await CheckUserInteractionsAndUpdateBalances(application, user, filter.CensorString(reason), "givebucks", Math.Round(amount, 2));
		}

		[Command("takebucks")]
		[Summary("Takes a specified amount of Wokebucks from another user's Wokebucks balance (allowed once per five minutes per unique user).")]
		public async Task TakeWokebuckAsync(
			[Summary("The amount of Wokebucks you want to take, between 1 and 5.")]
			double amount,
			[Summary("The reason you are taking their Wokebucks.")]
			string reason,
			[Summary("The user whose balance you want to take Wokebucks from.")]
			SocketUser? target = null)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "takebucks", Context.User.GetFullUsername());
			
			SocketUser? user = target ?? Context.Message.MentionedUsers.FirstOrDefault();
			var embedBuilder = new EmbedBuilder();
			if (user is null)
			{
				await RespondWithFormattedError(embedBuilder, $"You have to mention a user in order to remove from their balance.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting unknown user.", "takebucks", Context.User.GetFullUsername());
				return;
			}

			IApplication application = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(continueOnCapturedContext: false);
			if (double.IsNaN(amount) || amount < 0.01 || (amount > 5 && application.Owner.Id != Context.User.Id))
			{
				await RespondWithFormattedError(embedBuilder, $"You must select an amount between $0.01 and $5.00.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> attempting to assign an invalid value: <{amount}>.", "takebucks", Context.User.GetFullUsername());
				return;
			}

			if (await ReactIfSelfWhereNotAllowedAsync(application, user, Context.Message))
			{
				return;
			}

			var filter = new ProfanityFilter.ProfanityFilter();
			await CheckUserInteractionsAndUpdateBalances(application, user, filter.CensorString(reason), "takebucks", Math.Round(amount * -1, 2));
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

		[Command("transactions")]
		[Summary("Checks your last ten transactions.")]
		public async Task CheckTransactionsAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "transactions", Context.User.GetFullUsername());

			UserData? userData = await _documentClient.GetDocumentAsync<UserData>(Context.User.GetFullDatabaseId());

			if (userData is null)
			{
				userData = new UserData(Context.User.GetFullDatabaseId());
				await _documentClient.UpsertDocumentAsync<UserData>(userData);
			}

			var embedBuilder = new EmbedBuilder();
			embedBuilder.WithColor((userData.IsOverdrawn() ? Color.Red : Color.Green));
			embedBuilder.WithTitle($"{Context.User.GetFullUsername()}'s Latest Transactions");
			foreach (var transaction in userData.TransactionLog)
			{
				embedBuilder.AddField($"[{transaction.TimeStamp.DateTime}] **{transaction.TransactionInitiator}** {((transaction.Amount > 0) ? "gave" : "took")} ${Math.Abs(transaction.Amount)}.", $"{transaction.Comment}");
			}
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Transactions Request handled by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

			await ReplyAsync($"", false, embed: embedBuilder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "transactions", Context.User.GetFullUsername());
		}

		private async Task<bool> ReactIfSelfWhereNotAllowedAsync(IApplication application, SocketUser target, SocketUserMessage userMessage)
        {
			if (Context.User.Id != application.Owner.Id && string.Equals(Context.User.GetFullUsername(), target.GetFullUsername()))
			{
				var embedBuilder = new EmbedBuilder();
				embedBuilder.WithColor(Color.Red);
				embedBuilder.WithTitle("Invalid Bank Transaction");
				embedBuilder.WithDescription("You can't change your own Wokebucks ~~dumbass~~.");
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Message provided by Wokebucks");
				embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

				await ReplyAsync($"", false, embed: embedBuilder.Build());

				var emote = _discordSocketClient.Guilds
							.Where(x => x.Name == "The Swamp")
							.SelectMany(x => x.Emotes)
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

		private async Task CheckUserInteractionsAndUpdateBalances(IApplication application, SocketUser target, string reason, string commandName, double amount)
        {
			SocketUser caller = Context.User;

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
			double minutesSinceLastInteractionWithOtherUser = Context.User.Id != application.Owner.Id ? callerData.GetMinutesSinceLastUserInteractionTime(target.GetFullDatabaseId()) : double.MaxValue;
			if (minutesSinceLastInteractionWithOtherUser < 5)
			{
				// If 5 minutes has not passed, send message saying they have not waited at least 5 min since their last Wokebuck gift, and that x minutes are remaining (The Brad Clauses)
				await RespondWithFormattedError(embedBuilder, $"Sorry, you have to wait at least **{5 - (int)minutesSinceLastInteractionWithOtherUser} minutes** before you can give Wokebucks to or remove Wokebucks from **{target.GetFullUsername()}**'s balance.");

				_logger.LogInformation($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting user <{{{TargetUserIdKey}}}>.", commandName, Context.User.GetFullDatabaseId(), target.GetFullUsername());
			}
			else // minutesSinceLastInteractionWithOtherUser >= 5
			{
				// Let the bot owner do whatever amount, others can only subtract at most 5 or add at most 10
				if (Context.User.Id != application.Owner.Id && (amount > 10 || amount < -5))
                {
					// If amount needs to be within bounds for normal users
					await RespondWithFormattedError(embedBuilder, $"Sorry, you have select a value larger than $-5.00 or smaller than $10.00.");

					_logger.LogInformation($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting user <{{{TargetUserIdKey}}}>: Wokebucks value invalid.", commandName, Context.User.GetFullDatabaseId(), target.GetFullUsername());
					return;
				}

				UserData targetData = await _documentClient.GetDocumentAsync<UserData>(target.GetFullDatabaseId()) ?? new UserData(target.GetFullDatabaseId());
				targetData.AddToBalance(amount);
				targetData.AddTransaction(Context.User.GetFullUsername(), reason, amount);

				// Update leaderboard
				leaderboard.UpdateLeaderboard(Context, target, targetData.Balance);

				callerData.UpdateMostRecentInteractionForUser(target.GetFullDatabaseId());
				Task updateTargetDataTask = _documentClient.UpsertDocumentAsync<UserData>(targetData);
				Task updateCallerDataTask = !string.Equals(Context.User.GetFullUsername(), target.GetFullUsername()) ? _documentClient.UpsertDocumentAsync<UserData>(callerData) : Task.CompletedTask;
				Task updateLeaderboard = _documentClient.UpsertDocumentAsync<Leaderboard>(leaderboard);

				await Task.WhenAll(updateTargetDataTask, updateCallerDataTask, updateLeaderboard);

				embedBuilder.WithColor((targetData.IsOverdrawn() ? Color.Red : Color.Green));
				embedBuilder.WithTitle("Bank Transaction");
				embedBuilder.AddField($"{(commandName.Contains("take") ? "Victim" : "Recipient")}", $"{target.GetFullUsername()}", true);
				embedBuilder.AddField("Updated Balance", "$" + string.Format("{0:0.00}", targetData.Balance), true);
				embedBuilder.AddField("Reason", $"{reason}", true);
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Transaction provided by Wokebucks");
				embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

				await ReplyAsync($"", false, embed: embedBuilder.Build());

				_logger.LogInformation($"<{{{CommandName}}}> command successfully completed by user <{{{UserIdKey}}}> for user <{{{TargetUserIdKey}}}> with updated balance <{targetData.Balance}>.", commandName, Context.User.GetFullDatabaseId(), targetData.ID);
			}
		}

		private Task RespondWithFormattedError(EmbedBuilder builder, string message)
        {
			builder.WithColor(Color.Red);
			builder.WithTitle("Invalid Bank Transaction");
			builder.WithDescription(message);
			builder.WithFooter($"{Context.User.GetFullUsername()}'s Message provided by Wokebucks");
			builder.WithUrl("https://github.com/chicklightning/WokebucksBot");

			return ReplyAsync($"", false, embed: builder.Build());
		}
	}
}
