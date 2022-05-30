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

			await CheckUserInteractionsAndUpdateBalances(Context, user, "givebuck");
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

			await CheckUserInteractionsAndUpdateBalances(Context, user, "givebucks");
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

			await CheckUserInteractionsAndUpdateBalances(Context, user, "takebuck");
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

			await CheckUserInteractionsAndUpdateBalances(Context, user, "takebucks");
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

		private async Task CheckUserInteractionsAndUpdateBalances(SocketCommandContext context, SocketUser target, string commandName)
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

			// Bot owner can call commands unlimited times
			IApplication application = await context.Client.GetApplicationInfoAsync().ConfigureAwait(continueOnCapturedContext: false);
			double minutesSinceLastInteractionWithOtherUser = !(context.User.Id != application.Owner.Id) ? callerData.GetMinutesSinceLastUserInteractionTime(target.GetFullDatabaseId()) : double.MaxValue;
			if (minutesSinceLastInteractionWithOtherUser < 5)
			{
				// If 5 minutes has not passed, send message saying they have not waited at least 5 min since their last Wokebuck gift, and that x minutes are remaining
				var embedBuilder = new EmbedBuilder();
				embedBuilder.WithColor(Color.Red);
				embedBuilder.WithTitle("Invalid Bank Transaction");
				embedBuilder.WithDescription($"Sorry, you have to wait at least **{5 - (int)minutesSinceLastInteractionWithOtherUser} minutes** before you can give Wokebucks to or remove Wokebucks from **{target.GetFullUsername()}**'s balance.");
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Message provided by Wokebucks");
				embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

				await ReplyAsync($"", false, embed: embedBuilder.Build());

				_logger.LogInformation($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting user <{{{TargetUserIdKey}}}>.", commandName, Context.User.GetFullDatabaseId(), target.GetFullUsername());
			}
			else // minutesSinceLastInteractionWithOtherUser >= 60
			{
				// If hour has passed, allow user to give other user a Wokebuck, send an updated balance for the other user, and update most recent interaction for the caller
				UserData targetData = await _documentClient.GetDocumentAsync<UserData>(target.GetFullDatabaseId()) ?? new UserData(target.GetFullDatabaseId());
				
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

				// Update leaderboard
				leaderboard.UpdateLeaderboard(target.GetFullUsername(), targetData.Balance);

				callerData.UpdateMostRecentInteractionForUser(target.GetFullDatabaseId());
				Task updateTargetDataTask = _documentClient.UpsertDocumentAsync<UserData>(targetData);
				Task updateCallerDataTask = _documentClient.UpsertDocumentAsync<UserData>(callerData);
				Task updateLeaderboard = _documentClient.UpsertDocumentAsync<Leaderboard>(leaderboard);

				await Task.WhenAll(updateTargetDataTask, updateCallerDataTask, updateLeaderboard);

				var embedBuilder = new EmbedBuilder();
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
