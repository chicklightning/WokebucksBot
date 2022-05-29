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
			SocketUser user)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "givebuck", Context.User.GetFullUsername());

			await CheckUserInteractionsAndUpdateBalances(Context.User, user, "givebuck");
		}

		[Command("givebucks")]
		[Summary("Adds $10 to another user's Wokebucks balance (allowed once per two hours per unique user).")]
		[Alias("givebig", "addmuch")]
		public async Task GiveWokebucksAsync(
			[Summary("The user whose balance you want to add Wokebucks to.")]
			SocketUser user)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "givebucks", Context.User.GetFullUsername());

			await CheckUserInteractionsAndUpdateBalances(Context.User, user, "givebucks");
		}

		[Command("takebuck")]
		[Summary("Takes $1 from another user's Wokebucks balance (allowed once per hour per unique user).")]
		[Alias("take", "remove")]
		public async Task TakeWokebuckAsync(
			[Summary("The user whose balance you want to take a Wokebuck from.")]
			SocketUser user)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "takebuck", Context.User.GetFullUsername());

			await CheckUserInteractionsAndUpdateBalances(Context.User, user, "takebuck");
		}

		[RequireOwner]
		[Command("takebucks")]
		[Summary("Takes $10 from another user's Wokebucks balance (allowed once per two hours per unique user).")]
		[Alias("take", "remove")]
		public async Task TakeWokebucksAsync(
			[Summary("The user whose balance you want to take Wokebucks from.")]
			SocketUser user)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "takebucks", Context.User.GetFullUsername());

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

			await ReplyAsync($"Your balance is ${userData.Balance}.");
		}

		private async Task CheckUserInteractionsAndUpdateBalances(SocketUser caller, SocketUser target, string commandName)
        {
			if (string.Equals(caller.GetFullUsername(), target.GetFullUsername()))
            {
				await ReplyAsync("You can't give yourself Wokebucks dumbass.");
				return;
            }

			// Check user's relationship to other user to make sure at least an hour has passed
			UserData? callerData = await _documentClient.GetUserDataAsync(caller.GetFullDatabaseId()) ?? new UserData(caller.GetFullDatabaseId());
			double minutesSinceLastInteractionWithOtherUser = callerData.GetMinutesSinceLastUserInteractionTime(target.GetFullDatabaseId());
			if (minutesSinceLastInteractionWithOtherUser < 60)
			{
				// If an hour has not passed, send message saying they have not waited an hour since their last Wokebuck gift, and that x minutes are remaining
				await ReplyAsync($"Sorry, you have to wait at least {60 - minutesSinceLastInteractionWithOtherUser} before you can give Wokebucks to or remove Wokebucks from {target.GetFullUsername()}'s balance.");
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

				await ReplyAsync($"New Wokebucks balance for {target.GetFullUsername()} is ${targetData.Balance}.");

				_logger.LogInformation($"<{{{CommandName}}}> command successfully completed by user <{{{UserIdKey}}}> for user <{{{TargetUserIdKey}}}> with updated balance <{targetData.Balance}>.", commandName, Context.User.GetFullDatabaseId(), targetData.ID);
			}
		}
	}
}
