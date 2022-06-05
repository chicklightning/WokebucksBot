using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Swamp.WokebucksBot.CosmosDB;
using Swamp.WokebucksBot.Bot.Extensions;

namespace Swamp.WokebucksBot.Bot.CommandModules
{
    public class BettingModule : InteractionModuleBase<SocketInteractionContext>
	{
		private const string CommandName = "CommandName";
		private const string UserIdKey = "UserId";

		private readonly ILogger<BettingModule> _logger;
		private readonly CosmosDBClient _documentClient;

		public BettingModule(ILogger<BettingModule> logger, CosmosDBClient docClient)
		{
			_logger = logger;
			_documentClient = docClient;
		}

		[SlashCommand("startbet", "Start a bet.")]
		public async Task StartBetAsync(
			[Summary(name: "reason", description: "Name of (reason for) the bet.")] string bettingReason,
			[Summary(name: "options", description: "Comma-separated list of options.")] string optionsString)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "startbet", Context.User.GetFullUsername());
			await DeferAsync();

			var embedBuilder = new EmbedBuilder();
			if (string.IsNullOrWhiteSpace(bettingReason))
			{
				await FollowupWithFormattedError(Context.User, embedBuilder, $"You must provide a reason for the bet, since this will be the name of the bet.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since user did not provide a bet reason.", "startbet", Context.User.GetFullUsername());
				return;
			}

			List<string> options = optionsString.Split(',').ToList();
			if (options.Count() <= 1 || options.Count() > 6)
            {
				await FollowupWithFormattedError(Context.User, embedBuilder, $"You have to provide at least two (and no more than six) options to start a bet.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since user did not provide enough options for bet, or provided too many.", "startbet", Context.User.GetFullUsername());
				return;
			}

			// Create a bet using reason and option strings
			var reducedReason = bettingReason.Length > 200 ? bettingReason.Substring(0, 200) : bettingReason;
			var bet = new Bet(reducedReason, Context.User);
			
			try
            {
				bet.AddOptions(options);
			}
			catch (Exception e)
            {
				await FollowupWithFormattedError(Context.User, embedBuilder, $"You have to provide text for all options.");
				_logger.LogError(e, $"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since user provided a null or empty option.", "startbet", Context.User.GetFullUsername());
				return;
			}

			// Write bet to database
			await _documentClient.UpsertDocumentAsync<Bet>(bet);

			// Return multiple options for user to select, along with bet amount
			var menuBuilder = new SelectMenuBuilder()
									.WithPlaceholder("Select an option to place your bet on.")
									.WithCustomId(bet.ID);
			int count = 1;
			foreach (string option in bet.OptionTotals.Keys)
            {
				// Display option, underlying value is combined bet ID and option ID
				menuBuilder.AddOption(option, new Bet.BetOptionKey(bet.ID, option, Context.Guild.Id.ToString()).FullKey);
				count++;
            }

			var builder = new ComponentBuilder()
				.WithSelectMenu(menuBuilder);

			embedBuilder.WithColor(Color.Gold);
			embedBuilder.WithTitle($"Starting Bet");
			embedBuilder.AddField("Bet", $"{bet.Reason}");
			embedBuilder.AddField("Started By", $"{Context.User.GetFullUsername()}");
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Bet handled by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
			embedBuilder.WithCurrentTimestamp();

			await FollowupAsync("", embed: embedBuilder.Build(), components: builder.Build());
		}

		[SlashCommand("endbet", "End a bet.")]
		public async Task EndBetAsync(
            [Summary(name: "reason", description: "Name of (reason for) the bet.")] string bettingReason,
			[Summary(name: "winningoption", description: "Name the option that won.")] string option)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "endbet", Context.User.GetFullUsername());
			await DeferAsync();

			var embedBuilder = new EmbedBuilder();
			if (string.IsNullOrWhiteSpace(bettingReason))
			{
				await FollowupWithFormattedError(Context.User, embedBuilder, "You have to give the name of the bet (aka the betting reason).");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since bet name was empty or whitespace.", "endbet", Context.User.GetFullUsername());
				return;
			}

			if (string.IsNullOrWhiteSpace(option))
			{
				await FollowupWithFormattedError(Context.User, embedBuilder, "You have to provide the name of the winning option.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> tsince winning option name was empty or whitespace.", "givebucks", Context.User.GetFullUsername());
				return;
			}

			// Get bet from database and make sure reason was accurately provided (results in a valid bet)
			Bet? bet = await _documentClient.GetDocumentAsync<Bet>(Bet.CreateDeterministicGUIDFromReason(bettingReason));
			if (bet is null)
            {
				await FollowupWithFormattedError(Context.User, embedBuilder, "No bet with this reason exists (you may have misspelled something). This command is case-**insensitive**.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since no bet with given reason existed.", "endbet", Context.User.GetFullUsername());
				return;
			}

			IApplication application = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(continueOnCapturedContext: false);
			if (!string.Equals(bet.OwnerId, Context.User.Id.ToString()) && !string.Equals(Context.User.Id, application.Owner.Id))
            {
				await FollowupWithFormattedError(Context.User, embedBuilder, "You must be the owner of this bet to end it.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since user does not own this bet.", "endbet", Context.User.GetFullUsername());
				return;
			}

			if (!bet.OptionTotals.ContainsKey(option.Trim()))
			{
				await FollowupWithFormattedError(Context.User, embedBuilder, "No option with this name exists for this bet.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since no option with the given name exists for this bet.", "endbet", Context.User.GetFullUsername());
				return;
			}

			// Select the winning option and divvy out money to all users who bet
			IDictionary<string, double> winnersAndWinnings = await ReconcileBalancesAsync(bet, option, Context.Guild.Id.ToString());

			// Delete bet from db
			await _documentClient.DeleteDocumentAsync<Bet>(bet.ID);

			// Say the bet is over announcing the total winnings and users
			embedBuilder.WithColor(Color.Gold);
			embedBuilder.WithTitle($"Ending Bet");
			embedBuilder.AddField("Bet", $"{bet.Reason}");
			embedBuilder.WithFooter($"{bet.OwnerUsername}'s Bet handled by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
			embedBuilder.WithCurrentTimestamp();

			foreach (var winnerAndWinning in winnersAndWinnings)
            {
				embedBuilder.AddField($"{winnerAndWinning.Key} won!", $"${winnerAndWinning.Value}");
            }

			await FollowupAsync("", embed: embedBuilder.Build());
		}

		private async Task<IDictionary<string, double>> ReconcileBalancesAsync(Bet bet, string option, string guildId)
		{
			// Go through each option and get all the users who voted for that option and their individual wager amounts
			var getUsersThatBet = new List<Task<UserData?>>();
			foreach (var wager in bet.Wagers.Values)
			{
				string userId = wager.UserID;
				getUsersThatBet.Add(_documentClient.GetDocumentAsync<UserData>(userId));
			}

			try
			{
				await Task.WhenAll(getUsersThatBet);
			}
			catch (Exception e)
			{
				if (e is AggregateException)
				{
					_logger.LogError(e.InnerException, $"<{{{CommandName}}}> partially or fully failed to fetch all user documents.", "endbet");
				}
				else
                {
					_logger.LogError(e, $"<{{{CommandName}}}> partially or fully failed to fetch all user documents.", "endbet");
				}
			}

			// Get Leaderboard
			Leaderboard? leaderboard = await _documentClient.GetDocumentAsync<Leaderboard>("leaderboard");
			if (leaderboard is null)
            {
				_logger.LogError($"<{{{CommandName}}}> failed to fetch leaderboard.", "endbet");
				throw new NullReferenceException("Failed to fetch leaderboard.");
			}

			// Get total winnings:
			double winnings = 0;
			foreach (var optionTotal in bet.OptionTotals)
			{
				winnings += optionTotal.Value.OptionTotal;
			}

			// Add all relevant user documents to a list and calculate new totals
			var userData = new List<UserData>();
			var winnersAndWinnings = new Dictionary<string, double>();
			foreach (var getBetInfo in getUsersThatBet)
			{
				UserData? userDatum = await getBetInfo;
				if (userDatum is not null)
				{
					double wagerAmount = bet.Wagers[userDatum.ID].Amount;

					// if that user bet on the winning option, divide the option winnings and add to their account
					string optionName = bet.Wagers[userDatum.ID].Option;
					if (string.Equals(optionName, option))
                    {
						// Get percentage of option total they contributed
						double percentage = Math.Round(wagerAmount / bet.OptionTotals[optionName].OptionTotal, 2);
						double userWinnings = Math.Round(percentage * winnings);

						userDatum.AddToBalance(userWinnings);
						userDatum.AddTransaction("Wokebucks Bet", "Won the bet", userWinnings);

						winnersAndWinnings.Add(userDatum.Username, userWinnings);
					}

					userData.Add(userDatum);
				}
			}

			// Write the data back out to the user documents and to leaderboard
			var writeUsersThatBet = new List<Task>();
			foreach (UserData? userDatum in userData)
			{
				writeUsersThatBet.Add(_documentClient.UpsertDocumentAsync<UserData>(userDatum));
				leaderboard.ReconcileLeaderboard(userDatum.ID, userDatum.Balance, guildId);
			}
			writeUsersThatBet.Add(_documentClient.UpsertDocumentAsync(leaderboard));

			try
			{
				await Task.WhenAll(writeUsersThatBet);
			}
			catch (Exception e)
			{
				if (e is AggregateException)
				{
					_logger.LogError(e.InnerException, $"<{{{CommandName}}}> partially or fully failed to write all user or leaderboard documents.", "endbet");
				}
				else
				{
					_logger.LogError(e, $"<{{{CommandName}}}> partially or fully failed to write all user or leaderboard documents.", "endbet");
				}
			}

			return winnersAndWinnings;
		}

		private Task FollowupWithFormattedError(SocketUser user, EmbedBuilder builder, string message)
		{
			builder.WithColor(Color.Red);
			builder.WithTitle("Invalid Bank Transaction");
			builder.WithDescription(message);
			builder.WithFooter($"{user.GetFullUsername()}'s Message provided by Wokebucks");
			builder.WithCurrentTimestamp();
			builder.WithUrl("https://github.com/chicklightning/WokebucksBot");

			return FollowupAsync($"", embed: builder.Build());
		}
	}
}
