﻿using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Swamp.WokebucksBot.CosmosDB;
using Swamp.WokebucksBot.Bot.Extensions;
using System.Diagnostics;
using System.Reflection;

namespace Swamp.WokebucksBot.Bot
{
    public class DiscordClient : IDisposable
	{
		private const string ElapsedTimeKey = "ElapsedTime";
		private const string CommandName = "CommandName";
		private const string UserIdKey = "UserId";

		private readonly IServiceProvider _services;
		private readonly CommandService _commands;
		private readonly DiscordSocketClient _discordSocketClient;
		private readonly ILogger<DiscordClient> _logger;
		private InteractionService _interactionService;
		private CosmosDBClient _documentClient;
		private bool _isInitialized = false;
		private bool _disposed = false;

		private SocketMessage? _lastCommand = null;
		
		public DiscordClient(ILogger<DiscordClient> logger, IServiceProvider serviceProvider, DiscordSocketClient socketClient, CommandService commandService, CosmosDBClient cosmosDBClient)
		{
			_logger = logger;
			_services = serviceProvider;
			_commands = commandService;
			_discordSocketClient = socketClient;
			_documentClient = cosmosDBClient;
		}

		public async Task InitializeAsync(string discordToken)
		{
			if(!_isInitialized)
			{
				_isInitialized = true;

				_discordSocketClient.Log += Log;
				_discordSocketClient.MessageReceived += HandleCommandAsync;

				var stopwatch = new Stopwatch();
				stopwatch.Start();
				await _discordSocketClient.LoginAsync(TokenType.Bot, discordToken);
				stopwatch.Stop();
				_logger.LogInformation($"Login successful | {{{ElapsedTimeKey}}} ms.", stopwatch.ElapsedMilliseconds);

				stopwatch.Restart();
				await _discordSocketClient.StartAsync();
				stopwatch.Stop();
				_logger.LogInformation($"Successfully started DiscordSocketClient | {{{ElapsedTimeKey}}} ms.", stopwatch.ElapsedMilliseconds);

				_discordSocketClient.Ready += ClientReadyAsync;

				// Pass the service provider to the second parameter of
				// AddModulesAsync to inject dependencies to all modules 
				// that may require them.
				await _commands.AddModulesAsync(
					assembly: Assembly.GetEntryAssembly(),
					services: _services);

				_discordSocketClient.MessageReceived += HandleCommandAsync;
				_discordSocketClient.ButtonExecuted += HandleButtonAsync;
				_discordSocketClient.JoinedGuild += JoinedGuildAsync;
				_discordSocketClient.SelectMenuExecuted += BetMenuHandler;
				_discordSocketClient.ModalSubmitted += BetModalHandler;

                _commands.CommandExecuted += OnCommandExecutedAsync;
			}
		}

		public async Task HandleCommandAsync(SocketMessage socketMessage)
		{
            // Don't process the command if it was a system message or if it was a duplicate command (they seem to get sent every time, not sure why)
            if (socketMessage is not SocketUserMessage message ||
				(_lastCommand is not null && string.Equals(_lastCommand.CleanContent, socketMessage.CleanContent) && (socketMessage.Timestamp - _lastCommand.Timestamp).TotalSeconds < 4))
            {
                return;
            }

			_lastCommand = socketMessage;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

			// Determine if the message is a command based on the prefix and make sure no bots trigger commands
			if (!(message.HasCharPrefix('$', ref argPos) ||
				message.HasMentionPrefix(_discordSocketClient.CurrentUser, ref argPos)) ||
				message.Author.IsBot)
			{
				return;
			}

			// Create a WebSocket-based command context based on the message
			var context = new SocketCommandContext(_discordSocketClient, message);

			// Pass the service provider to the ExecuteAsync method for
			// precondition checks.
			await _commands.ExecuteAsync(
				context: context,
				argPos: argPos,
				services: _services);
		}

		public async Task ClientReadyAsync()
		{
			_interactionService = new InteractionService(_discordSocketClient, new InteractionServiceConfig()
            {
				UseCompiledLambda = true
            });
			await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
			await _interactionService.RegisterCommandsGloballyAsync();

			_discordSocketClient.InteractionCreated += async interaction =>
			{
				var ctx = new SocketInteractionContext(_discordSocketClient, interaction);
				await _interactionService.ExecuteCommandAsync(ctx, _services);
			};
		}

		public async Task JoinedGuildAsync(SocketGuild guild)
        {
			// Create new lottery for this guild if it doesn't exist already
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by guild <{{{UserIdKey}}}>.", "joinguild", guild.Id.ToString());
			Lottery? lottery = await _documentClient.GetDocumentAsync<Lottery>(Lottery.FormatLotteryIdFromGuildId(guild.Id.ToString()));
			if (lottery is null)
            {
				lottery = new Lottery(guild.Id.ToString());
				await _documentClient.UpsertDocumentAsync<Lottery>(lottery);
            }

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by guild <{{{UserIdKey}}}>.", "joinguild", guild.Id.ToString());
		}

		public async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, Discord.Commands.IResult result)
        {
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "resolveLottery", context.User.Id);

			// Get lotteries from all guilds the bot is in
			IDictionary<string, Task<Lottery?>> fetchLotteries = new Dictionary<string, Task<Lottery?>>();
			IDictionary<string, SocketGuild> socketGuilds = new Dictionary<string, SocketGuild>();
			foreach (SocketGuild guild in _discordSocketClient.Guilds)
			{
				socketGuilds[guild.Id.ToString()] = guild;
				fetchLotteries.Add(guild.Id.ToString(), _documentClient.GetDocumentAsync<Lottery>(Lottery.FormatLotteryIdFromGuildId(guild.Id.ToString())));
			}

			await Task.WhenAll(fetchLotteries.Values);

			Leaderboard? leaderboard = await _documentClient.GetDocumentAsync<Leaderboard>("leaderboard");
			if (leaderboard is null)
			{
				var e = new InvalidOperationException("Could not find leaderboard.");
				_logger.LogError(e, "Could not find leaderboard.");
				throw e;
			}

			IDictionary<string, Lottery?> guildsToLotteries = new Dictionary<string, Lottery?>();
			foreach (KeyValuePair<string, Task<Lottery?>> fetchLotteryTaskWithGuildId in fetchLotteries)
			{
				Lottery? lottery = await fetchLotteryTaskWithGuildId.Value;

				// If a lottery is null, add it anyway; we'll want to create a lottery for this guild
				guildsToLotteries.Add(fetchLotteryTaskWithGuildId.Key, lottery);
			}

			IDictionary<string, UserData> winners = new Dictionary<string, UserData>(); // Key is user ID, UserData is value
			IList<Task> writesLotteriesAndUsers = new List<Task>();
			foreach (KeyValuePair<string, Lottery?> guildToLottery in guildsToLotteries)
			{
				// All guilds should have lotteries, otherwise don't worry about it it'll get taken care of when the bot joins a guild
				if (guildToLottery.Value is not null)
				{
					string winnerId = guildToLottery.Value.GetWeightedRandomTotals();

					// If winner didn't already win today, fetch their document otherwise just grab it from the dictionary
					UserData userData;
					if (!winners.ContainsKey(winnerId))
					{
						userData = await _documentClient.GetDocumentAsync<UserData>($"{winnerId}") ?? throw new NullReferenceException($"User with id <{winnerId}> failed to be created prior to lottery reconciliation but still had a lottery ticket purchased.");
					}
					else
					{
						userData = winners[winnerId];
					}

					userData.AddToBalance(guildToLottery.Value.JackpotAmount);
					userData.AddTransaction("Wokebucks Lottery", "Won the lottery!", guildToLottery.Value.JackpotAmount);
					writesLotteriesAndUsers.Add(_documentClient.UpsertDocumentAsync<UserData>(userData));

					var embedBuilder = new EmbedBuilder()
											.WithColor(Color.Gold)
											.WithTitle("Lottery Results")
											.AddField("Jackpot Total", "$" + string.Format("{0:0.00}", guildToLottery.Value.JackpotAmount))
											.AddField("Winner", $"{userData.Username}")
											.WithFooter($"{socketGuilds[guildToLottery.Key].Name}'s Lottery handled by Wokebucks")
											.WithUrl("https://github.com/chicklightning/WokebucksBot")
											.WithCurrentTimestamp();

					await context.Channel.SendMessageAsync("", embed: embedBuilder.Build());

					leaderboard.ReconcileLeaderboard(userData.ID, userData.Balance, guildToLottery.Key);

					guildToLottery.Value.ResetLottery();
					writesLotteriesAndUsers.Add(_documentClient.UpsertDocumentAsync<Lottery>(guildToLottery.Value));
				}
			}

			await Task.WhenAll(writesLotteriesAndUsers);

			_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "resolveLottery", context.User.GetFullUsername());
		}

		public async Task HandleButtonAsync(SocketMessageComponent component)
		{
			// We can now check for our custom id
			if (component.Data.CustomId.Contains("lottery"))
			{
				_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "lotteryticket", component.User.GetFullUsername());

				Task<Leaderboard?> fetchLeaderboard = _documentClient.GetDocumentAsync<Leaderboard>("leaderboard");
				Task<Lottery?> fetchLottery = _documentClient.GetDocumentAsync<Lottery>(component.Data.CustomId);
				Task<UserData?> fetchUser = _documentClient.GetDocumentAsync<UserData>($"{component.User.Id}");

				await Task.WhenAll(fetchLeaderboard, fetchLottery, fetchUser);

				Lottery? lottery = await fetchLottery;
				if (lottery is null)
				{
					var e = new InvalidOperationException("Could not find lottery.");
					_logger.LogError(e, "Could not find lottery.");
					throw e;
				}

				Leaderboard? leaderboard = await fetchLeaderboard;
				if (leaderboard is null)
				{
					var e = new InvalidOperationException("Could not find leaderboard.");
					_logger.LogError(e, "Could not find leaderboard.");
					throw e;
				}

				UserData userData = await fetchUser ?? new UserData(component.User);
				userData.UpdateUsernameAndBalance(-1, component.User.GetFullUsername());
				userData.AddTransaction("Wokebucks Lottery", "Purchased a ticket", -1);

				leaderboard.UpdateLeaderboard(Lottery.GetGuildIdFromLotteryId(component.Data.CustomId), component.User, userData.Balance);

				lottery.AddTicketPurchase(component.User.Id.ToString());

				Task writeLottery = _documentClient.UpsertDocumentAsync<Lottery>(lottery);
				Task writeUser = _documentClient.UpsertDocumentAsync<UserData>(userData);
				Task writeLeaderboard = _documentClient.UpsertDocumentAsync<Leaderboard>(leaderboard);

				await Task.WhenAll(writeLottery, writeUser, writeLeaderboard);

				var embedBuilder = new EmbedBuilder()
										.WithColor(Color.Gold)
										.WithTitle("You Purchased a Lottery Ticket")
										.WithDescription($"You have bought {lottery.TicketsPurchased[component.User.Id.ToString()]} tickets.")
										.AddField("Jackpot Total", "$" + string.Format("{0:0.00}", lottery.JackpotAmount))
										.WithFooter($"{component.User.GetFullUsername()}'s Lottery Ticket Purchase handled by Wokebucks")
										.WithUrl("https://github.com/chicklightning/WokebucksBot")
										.WithCurrentTimestamp();

				await component.RespondAsync("", ephemeral: true, embed: embedBuilder.Build());

				_logger.LogInformation($"<{{{CommandName}}}> command successfully invoked by user <{{{UserIdKey}}}>.", "lotteryticket", component.User.GetFullUsername());
			}
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
				await modal.FollowUpWithErrorAsync(embedBuilder, modal.User, "This bet has ended.");
				_logger.LogError($"<{{{CommandName}}}> failed for user <{{{UserIdKey}}}> since bet has ended.", "addbetmodal", modal.User.GetFullUsername());
				return;
			}

			string betAmountString = component.Value;
			if (!Double.TryParse(betAmountString, out double betAmount) || Double.IsNaN(betAmount) || Double.IsInfinity(betAmount) || betAmount < 0.01 || betAmount > 20)
			{
				// Invalid bet amount
				await modal.FollowUpWithErrorAsync(embedBuilder, modal.User, "Invalid bet amount, you must bet between $0.01 and $20.00.");
				_logger.LogError($"<{{{CommandName}}}> add bet failed for user <{{{UserIdKey}}}> since bet amount was invalid.", "addbetmodal", modal.User.GetFullUsername());
				return;
			}

			// Bet is valid, write to db
			if (!bet.AddBet(betOptionKey.OptionId, modal.User, betAmount))
			{
				// Can't change bet
				await modal.FollowUpWithErrorAsync(embedBuilder, modal.User, "You have already made a wager for this bet.");
				_logger.LogError($"<{{{CommandName}}}> add bet failed for user <{{{UserIdKey}}}> since user has already bet.", "addbetmodal", modal.User.GetFullUsername());
				return;
			}

			// Check if user exists, if not, create them and add them to the leaderboard
			Task<UserData?> userDataTask = _documentClient.GetDocumentAsync<UserData>(modal.User.Id.ToString());

			Task<Leaderboard?> leaderboardTask = _documentClient.GetDocumentAsync<Leaderboard>("leaderboard");

			await Task.WhenAll(userDataTask, leaderboardTask);

			UserData? userData = await userDataTask;
			if (userData is null)
            {
				userData = new UserData(modal.User);
            }
			userData.UpdateUsernameAndBalance(-1 * betAmount, modal.User.GetFullUsername());
			userData.AddTransaction("Wokebucks Bet", $"Entered a wager: {bet.Reason}", -1.0 * betAmount);

			Leaderboard? leaderboard = await leaderboardTask;
			if (leaderboard is null)
			{
				var e = new InvalidOperationException("Could not find leaderboard.");
				_logger.LogError(e, "Could not find leaderboard.");
				throw e;
			}

			leaderboard.UpdateLeaderboard(betOptionKey.GuildId, modal.User, userData.Balance);

			Task writeBet = _documentClient.UpsertDocumentAsync<Bet>(bet);
			Task writeLeaderboard = _documentClient.UpsertDocumentAsync<Leaderboard>(leaderboard);
			Task writeUserData = _documentClient.UpsertDocumentAsync<UserData>(userData);

			await Task.WhenAll(writeBet, writeLeaderboard, writeUserData);

			// Respond to the modal.
			embedBuilder.WithColor(Color.Blue);
			embedBuilder.WithTitle($"Wager Entered");
			embedBuilder.AddField("Bet", $"{bet.Reason}");
			embedBuilder.AddField("Wager Made By", $"{modal.User.GetFullUsername()}");
			embedBuilder.AddField("Option Selected", $"{betOptionKey.OptionId}");
			embedBuilder.AddField("Bet Amount", "$" + string.Format("{0:0.00}", betAmount));
			embedBuilder.WithFooter($"{modal.User.GetFullUsername()}'s Wager handled by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
			embedBuilder.WithCurrentTimestamp();

			await modal.FollowupAsync("", embed: embedBuilder.Build());
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					_discordSocketClient?.Dispose();
				}

				_disposed = true;
			}
		}

		private Task Log(LogMessage msg)
		{
			LogLevel logLevel;
			switch (msg.Severity)
			{
				case LogSeverity.Debug:
					{
						logLevel = LogLevel.Debug;
						break;
					}
				case LogSeverity.Verbose:
					{
						logLevel = LogLevel.Trace;
						break;
					}
				case LogSeverity.Info:
					{
						logLevel = LogLevel.Information;
						break;
					}
				case LogSeverity.Warning:
					{
						logLevel = LogLevel.Warning;
						break;
					}
				case LogSeverity.Error:
					{
						logLevel = LogLevel.Error;
						break;
					}
				case LogSeverity.Critical:
					{
						logLevel = LogLevel.Critical;
						break;
					}
				default:
					{
						logLevel = LogLevel.None;
						break;
					}
			}

			_logger.Log(logLevel, msg.Exception, msg.Message);
			return Task.CompletedTask;
		}
	}
}
