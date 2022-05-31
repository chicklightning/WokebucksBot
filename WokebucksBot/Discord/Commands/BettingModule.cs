using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Swamp.WokebucksBot.CosmosDB;

namespace Swamp.WokebucksBot.Discord.Commands
{
    public class BettingModule : InteractionModuleBase<SocketInteractionContext>
	{
		private const string CommandName = "CommandName";
		private const string UserIdKey = "UserId";

		private readonly ILogger<BettingModule> _logger;
		private readonly CosmosDBClient _documentClient;
		private readonly DiscordSocketClient _discordSocketClient;

		public BettingModule(ILogger<BettingModule> logger, DiscordSocketClient discordSocketClient, CosmosDBClient docClient)
		{
			_logger = logger;
			_discordSocketClient = discordSocketClient;
			_documentClient = docClient;

			_discordSocketClient.SelectMenuExecuted += BetMenuHandler;
			_discordSocketClient.ModalSubmitted += BetModalHandler;
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
			var bet = new Bet(reducedReason, Context.User.GetFullDatabaseId());
			
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
				menuBuilder.AddOption(option, new Bet.BetOptionKey(bet.ID, option).FullKey);
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
			if (!string.Equals(bet.OwnerId, Context.User.GetFullDatabaseId()) || !string.Equals(Context.User.Id, application.Owner.Id))
            {
				await FollowupWithFormattedError(Context.User, embedBuilder, "You must be the owner of this bet to end it.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since user does not own this bet.", "endbet", Context.User.GetFullUsername());
				return;
			}

			if (!bet.OptionTotals.ContainsKey(option))
			{
				await FollowupWithFormattedError(Context.User, embedBuilder, "No option with this name exists for this bet.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since no option with the given name exists for this bet.", "endbet", Context.User.GetFullUsername());
				return;
			}

			// Select the winning option and divvy out money to all users who bet
			await FollowupAsync("Ending bet and reconciling balances...");
			IDictionary<string, double> winnersAndWinnings = await ReconcileBalancesAsync(bet, option, Context.Guild.Id.ToString());

			// Delete bet from db
			await _documentClient.DeleteDocumentAsync<Bet>(bet.ID);

			// Say the bet is over announcing the total winnings and users
			embedBuilder.WithColor(Color.Gold);
			embedBuilder.WithTitle($"Ending Bet");
			embedBuilder.AddField("Bet", $"{bet.Reason}");
			embedBuilder.AddField("Started By", $"{SocketUserExtensions.SwitchToUsername(bet.OwnerId)}");
			embedBuilder.WithFooter($"{SocketUserExtensions.SwitchToUsername(bet.OwnerId)}'s Bet handled by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

			foreach (var winnerAndWinning in winnersAndWinnings)
            {
				embedBuilder.AddField($"{winnerAndWinning.Key} won!", $"${winnerAndWinning.Value}");
            }

			await FollowupAsync("", embed: embedBuilder.Build());
		}

		public async Task BetMenuHandler(SocketMessageComponent component)
		{
			var betOptionFullKey = string.Join(", ", component.Data.Values);
			var betOptionKey = new Bet.BetOptionKey(betOptionFullKey);

			// Set up modal to let user add bet amount
			var tb = new TextInputBuilder()
							.WithLabel("Bet Amount")
							.WithCustomId($"{betOptionKey.FullKey}")
							.WithStyle(TextInputStyle.Short)
							.WithMinLength(1)
							.WithMaxLength(3)
							.WithRequired(true)
							.WithPlaceholder("Write \"1\" to bet $1.");

			var mb = new ModalBuilder()
							.WithTitle("Bet Amount")
							.WithCustomId($"{betOptionKey.FullKey}")
							.AddTextInput(tb);

			await component.RespondWithModalAsync(mb.Build());
		}

		public async Task BetModalHandler(SocketModal modal)
        {
			await modal.DeferAsync();

			// Get the values of components
			List<SocketMessageComponentData> components = modal.Data.Components.ToList();

			// Get bet option IDs and check if bet is still running
			SocketMessageComponentData component = components[0];
			string betOptionString = component.CustomId;
			var betOptionKey = new Bet.BetOptionKey(betOptionString);
			Bet? bet = await _documentClient.GetDocumentAsync<Bet>(betOptionKey.BetId);

			var embedBuilder = new EmbedBuilder();
			if (bet is null)
            {
				// Bet is over, tell them they can no longer bet
				await FollowupWithFormattedError(modal.User, embedBuilder, "This bet has ended.");
				_logger.LogError($"<{{{CommandName}}}> failed for user <{{{UserIdKey}}}> since bet has ended.", "addbetmodal", modal.User.GetFullUsername());
				return;
			}

			string betAmountString = component.Value;
			if (!Double.TryParse(betAmountString, out double betAmount) || Double.IsNaN(betAmount) || Double.IsInfinity(betAmount) || betAmount < 0.01 || betAmount > 20)
            {
				// Invalid bet amount
				await FollowupWithFormattedError(modal.User, embedBuilder, "Invalid bet amount, you must bet between $0.01 and $20.00.");
				_logger.LogError($"<{{{CommandName}}}> add bet failed for user <{{{UserIdKey}}}> since bet amount was invalid.", "addbetmodal", modal.User.GetFullUsername());
				return;
			}

			// Bet is valid, write to db
			if (!bet.AddBet(betOptionKey.OptionId, modal.User.GetFullDatabaseId(), betAmount))
            {
				// Invalid bet amount
				await FollowupWithFormattedError(modal.User, embedBuilder, "You have already made a wager for this bet.");
				_logger.LogError($"<{{{CommandName}}}> add bet failed for user <{{{UserIdKey}}}> since user has already bet.", "addbetmodal", modal.User.GetFullUsername());
				return;
			}

			await _documentClient.UpsertDocumentAsync<Bet>(bet);

			// Respond to the modal.
			embedBuilder.WithColor(Color.Blue);
			embedBuilder.WithTitle($"Wager Entered");
			embedBuilder.AddField("Bet", $"{bet.Reason}");
			embedBuilder.AddField("Wagered Made By", $"{modal.User.GetFullUsername()}");
			embedBuilder.AddField("Option Selected", $"{betOptionKey.OptionId}");
			embedBuilder.AddField("Bet Amount", "$" + string.Format("{0:0.00}", betAmount));
			embedBuilder.WithFooter($"{modal.User.GetFullUsername()}'s Wager handled by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

			await modal.FollowupAsync("", embed: embedBuilder.Build());
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
					// Get that user's bet amount and remove it from their balance
					double wagerAmount = bet.Wagers[userDatum.ID].Amount;
					userDatum.AddToBalance(wagerAmount);
					userDatum.AddTransaction("Wokebucks Bet", $"Entered a wager: {bet.Reason}", -1.0 * wagerAmount);

					// if that user bet on the winning option, divide the option winnings and add to their account
					string optionName = bet.Wagers[userDatum.ID].Option;
					if (string.Equals(optionName, option))
                    {
						// Get percentage of option total they contributed
						double percentage = Math.Round(wagerAmount / bet.OptionTotals[optionName].OptionTotal, 2);
						double userWinnings = Math.Round(percentage * winnings);

						userDatum.AddToBalance(userWinnings);
						userDatum.AddTransaction("Wokebucks Bet", "Won the bet", userWinnings);

						winnersAndWinnings.Add(SocketUserExtensions.SwitchToUsername(userDatum.ID), userWinnings);
					}

					userData.Add(userDatum);
				}
			}

			// Write the data back out to the user documents and to leaderboard
			var writeUsersThatBet = new List<Task>();
			foreach (UserData userDatum in userData)
			{
				writeUsersThatBet.Add(_documentClient.UpsertDocumentAsync<UserData>(userDatum));
				leaderboard.UpdateLeaderboard(guildId, userDatum.ID, userDatum.Balance);
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
			builder.WithUrl("https://github.com/chicklightning/WokebucksBot");

			return FollowupAsync($"", embed: builder.Build());
		}
	}
}
