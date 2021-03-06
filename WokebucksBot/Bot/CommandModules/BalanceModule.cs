using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Swamp.WokebucksBot.CosmosDB;
using Swamp.WokebucksBot.Bot.Extensions;

namespace Swamp.WokebucksBot.Bot.CommandModules
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
			if (await ReactIfSelfWhereNotAllowedAsync(application, user, Context.Message))
			{
				return;
			}

			var filter = new ProfanityFilter.ProfanityFilter();
			var filteredstring = reason.Length > 200 ? reason.Substring(0, 200) : reason;
			filteredstring = filter.CensorString(filteredstring);
			await CheckUserInteractionsAndUpdateBalances(application, user, filteredstring, "givebucks", Math.Round(amount, 2));
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
			if (await ReactIfSelfWhereNotAllowedAsync(application, user, Context.Message))
			{
				return;
			}

			var filter = new ProfanityFilter.ProfanityFilter();
			var filteredstring = reason.Length > 200 ? reason.Substring(0, 200) : reason;
			filteredstring = filter.CensorString(filteredstring);
			await CheckUserInteractionsAndUpdateBalances(application, user, filteredstring, "takebucks", Math.Round(amount * -1, 2));
		}

		[Command("balance")]
		[Summary("Checks your balance.")]
		[Alias("check", "checkbucks")]
		public async Task CheckWokebucksAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "balance", Context.User.GetFullUsername());

			UserData? userData = await _documentClient.GetDocumentAsync<UserData>(Context.User.Id.ToString());

			if (userData is null)
			{
				userData = new UserData(Context.User);
				await _documentClient.UpsertDocumentAsync<UserData>(userData);
			}

			var embedBuilder = new EmbedBuilder();
			embedBuilder.WithColor((userData.IsOverdrawn() ? Color.Red : Color.Green));
			embedBuilder.WithTitle("$" + string.Format("{0:0.00}", userData.Balance));
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Balance provided by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
			embedBuilder.WithCurrentTimestamp();

			await ReplyAsync($"", false, embed: embedBuilder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "balance", Context.User.GetFullUsername());
		}

		[Command("transactions")]
		[Summary("Checks your last ten transactions.")]
		public async Task CheckTransactionsAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "transactions", Context.User.GetFullUsername());

			UserData? userData = await _documentClient.GetDocumentAsync<UserData>(Context.User.Id.ToString());

			if (userData is null)
			{
				userData = new UserData(Context.User);
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
			embedBuilder.WithCurrentTimestamp();

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
				embedBuilder.WithCurrentTimestamp();

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
			Task<UserData?> callerDataFetchTask = _documentClient.GetDocumentAsync<UserData>(caller.Id.ToString());
			Task<Leaderboard?> leaderboardFetchTask = _documentClient.GetDocumentAsync<Leaderboard>("leaderboard");
			Task<Lottery?> fetchLotteryTask = _documentClient.GetDocumentAsync<Lottery>($"{Lottery.FormatLotteryIdFromGuildId(Context.Guild.Id.ToString())}");

			await Task.WhenAll(callerDataFetchTask, leaderboardFetchTask, fetchLotteryTask);

			UserData callerData = await callerDataFetchTask ?? new UserData(caller);
			Leaderboard? leaderboard = await leaderboardFetchTask;
			Lottery? lottery = await fetchLotteryTask;

			if (leaderboard is null)
			{
				var e = new InvalidOperationException("Could not find leaderboard.");
				_logger.LogError(e, "Could not find leaderboard.");
				throw e;
			}

			if (lottery is null)
			{
				var e = new InvalidOperationException("Could not find lottery.");
				_logger.LogError(e, "Could not find lottery.");
				throw e;
			}

			var embedBuilder = new EmbedBuilder();

			if (double.IsNaN(amount))
			{
				await RespondWithFormattedError(embedBuilder, "You must select a valid amount of Wokebucks.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> attempting to assign an invalid value: <{amount}>.", commandName, Context.User.GetFullUsername());
				return;
			}

			// Bot owner can call commands unlimited times
			double minutesSinceLastInteractionWithOtherUser = Context.User.Id != application.Owner.Id ? callerData.GetMinutesSinceLastUserInteractionTime(target.Id.ToString()) : double.MaxValue;
			if (minutesSinceLastInteractionWithOtherUser < 5)
			{
				// If 5 minutes has not passed, send message saying they have not waited at least 5 min since their last Wokebuck gift, and that x minutes are remaining (The Brad Clauses)
				await RespondWithFormattedError(embedBuilder, $"Sorry, you have to wait at least **{5 - (int)minutesSinceLastInteractionWithOtherUser} minutes** before you can give Wokebucks to or remove Wokebucks from **{target.GetFullUsername()}**'s balance.");

				_logger.LogInformation($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting user <{{{TargetUserIdKey}}}>.", commandName, Context.User.GetFullUsername(), target.GetFullUsername());
			}
			else // minutesSinceLastInteractionWithOtherUser >= 5
			{
				// Let the bot owner do whatever amount
				double giveCapAmount = (callerData.Level > 0) ? Levels.AllLevels[callerData.Level].UpperLimit : 10;
				double takeCapAmount = (callerData.Level > 0) ? Levels.AllLevels[callerData.Level].LowerLimit : -5;
				if (string.Equals(commandName, "givebucks") && (amount < 0.01 || (amount > giveCapAmount && application.Owner.Id != Context.User.Id)))
				{
					await RespondWithFormattedError(embedBuilder, $"You must select an amount between $0.01 and ${string.Format("{0:0.00}", giveCapAmount)}.");
					_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> attempting to give an invalid value: <{amount}>.", commandName, Context.User.GetFullUsername());
					return;
				}
				else if (string.Equals(commandName, "takebucks") && (amount > -0.01 || (amount < takeCapAmount && application.Owner.Id != Context.User.Id)))
				{
					await RespondWithFormattedError(embedBuilder, $"You must select an amount between $0.01 and ${string.Format("{0:0.00}", takeCapAmount * -1)}.");
					_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> attempting to take an invalid value: <{amount}>.", commandName, Context.User.GetFullUsername());
					return;
				}

				UserData targetData = await _documentClient.GetDocumentAsync<UserData>(target.Id.ToString()) ?? new UserData(target);
				targetData.UpdateUsernameAndAddToBalance(amount, target.GetFullUsername());
				targetData.AddTransaction(Context.User.GetFullUsername(), reason, amount);

				// Update leaderboard
				leaderboard.UpdateLeaderboard(Context.Guild.Id.ToString(), target, targetData.Balance);

				// Add $1 to lottery pool:
				lottery.JackpotAmount += 1;

				callerData.UpdateMostRecentInteractionForUser(target.Id.ToString());
				Task updateTargetDataTask = _documentClient.UpsertDocumentAsync<UserData>(targetData);
				Task updateCallerDataTask = !string.Equals(Context.User.Id, target.Id) ? _documentClient.UpsertDocumentAsync<UserData>(callerData) : Task.CompletedTask;
				Task updateLeaderboard = _documentClient.UpsertDocumentAsync<Leaderboard>(leaderboard);
				Task updateLottery = _documentClient.UpsertDocumentAsync<Lottery>(lottery);

				await Task.WhenAll(updateTargetDataTask, updateCallerDataTask, updateLeaderboard, updateLottery);

				embedBuilder.WithColor((targetData.IsOverdrawn() ? Color.Red : Color.Green));
				embedBuilder.WithTitle("Bank Transaction");
				embedBuilder.AddField($"{(commandName.Contains("take") ? "Victim" : "Recipient")}", $"{target.GetFullUsername()}", true);
				embedBuilder.AddField("Updated Balance", "$" + string.Format("{0:0.00}", targetData.Balance), true);
				embedBuilder.AddField("Reason", $"{reason}", true);
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Transaction provided by Wokebucks");
				embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
				embedBuilder.WithCurrentTimestamp();

				await ReplyAsync($"", false, embed: embedBuilder.Build());

				_logger.LogInformation($"<{{{CommandName}}}> command successfully completed by user <{{{UserIdKey}}}> for user <{{{TargetUserIdKey}}}> with updated balance <{targetData.Balance}>.", commandName, Context.User.GetFullUsername(), targetData.ID);
			}
		}

		private Task RespondWithFormattedError(EmbedBuilder builder, string message)
        {
			builder.WithColor(Color.Red);
			builder.WithTitle("Invalid Bank Transaction");
			builder.WithDescription(message);
			builder.WithFooter($"{Context.User.GetFullUsername()}'s Message provided by Wokebucks");
			builder.WithCurrentTimestamp();
			builder.WithUrl("https://github.com/chicklightning/WokebucksBot");

			return ReplyAsync($"", false, embed: builder.Build());
		}
	}
}
