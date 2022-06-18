using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Swamp.WokebucksBot.Bot.Extensions;
using Swamp.WokebucksBot.CosmosDB;

namespace Swamp.WokebucksBot.Bot.CommandModules
{
	public class CancelModule : ModuleBase<SocketCommandContext>
	{
		private const string CommandName = "CommandName";
		private const string UserIdKey = "UserId";
		private const string TargetUserIdKey = "TargetUserId";

		private readonly ILogger<CancelModule> _logger;
		private readonly CosmosDBClient _documentClient;
		private readonly DiscordSocketClient _discordSocketClient;

		public CancelModule(ILogger<CancelModule> logger, DiscordSocketClient discordSocketClient, CosmosDBClient docClient)
		{
			_logger = logger;
			_discordSocketClient = discordSocketClient;
			_documentClient = docClient;
		}

		[Command("cancel")]
		[Summary("Puts up a vote for a user to be canceled (doubles a negative balance or sets a positive balance to 0). If you had a previous ticket against this user, this will overwrite that ticket.")]
		public async Task CancelUserAsync(
			[Summary("The reason you are canceling this user.")]
			string reason,
			[Summary("The user you want to cancel.")]
			SocketUser? target = null)
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "cancel", Context.User.GetFullUsername());

			var embedBuilder = new EmbedBuilder();
			if (string.IsNullOrWhiteSpace(reason))
            {
				await RespondWithFormattedError(embedBuilder, "You have to provide a reason for cancellation.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since they failed to provide a reason", "cancel", Context.User.GetFullUsername());
				return;
			}

			SocketUser? targetUser = target ?? Context.Message.MentionedUsers.FirstOrDefault();
			if (targetUser is null)
			{
				await RespondWithFormattedError(embedBuilder, "You have to mention a user in order to cancel them.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> targeting unknown user.", "cancel", Context.User.GetFullUsername());
				return;
			}

			IApplication application = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(continueOnCapturedContext: false);
			if (await ReactIfSelfWhereNotAllowedAsync(application, targetUser, Context.Message))
			{
				return;
			}

			// See if user has a current ticket against this user already:
			string ticketId = CancelTicket.CreateDeterministicTicketGuid(Context.User.Id.ToString(), targetUser.Id.ToString());
			CancelTicket? ticket = await _documentClient.GetDocumentAsync<CancelTicket>(ticketId);
			if (ticket is not null && ticket.TicketOpened.AddDays(2) > DateTimeOffset.UtcNow)
			{
				await RespondWithFormattedError(embedBuilder, "You have to wait at least two (2) days before opening a new ticket against this user.");
				_logger.LogError($"<{{{CommandName}}}> command failed for user <{{{UserIdKey}}}> since ticket from the last two days already exists for target.", "cancel", Context.User.GetFullUsername());
				return;
			}

			// It's been at least two days, so overwrite this ticket:
			Task<UserData?> fetchTargetData = _documentClient.GetDocumentAsync<UserData>(targetUser.Id.ToString());
			Task<UserData?> fetchInitiatorData = _documentClient.GetDocumentAsync<UserData>(Context.User.Id.ToString());

			await Task.WhenAll(fetchTargetData, fetchInitiatorData);
			UserData targetData = await fetchTargetData ?? new UserData(targetUser);
			UserData initiatorData = await fetchInitiatorData ?? new UserData(Context.User);

			// Add ticket to both users:
			var reducedReason = reason.Length > 200 ? reason.Substring(0, 200) : reason;
			ticket = new CancelTicket(targetUser, Context.User, reducedReason);
			initiatorData.AddCreatedTicket(ticket);
			targetData.AddCancelTicket(ticket);

			Task writeTicketTask = _documentClient.UpsertDocumentAsync<CancelTicket>(ticket);
			Task writeInitiatorTask = _documentClient.UpsertDocumentAsync<UserData>(initiatorData);
			Task writeTargetTask = _documentClient.UpsertDocumentAsync<UserData>(targetData);

			await Task.WhenAll(writeTicketTask, writeInitiatorTask, writeTargetTask);

			var ticketEmoji = new Emoji("\uD83D\uDEAB");
			var buttonBuilder = new ButtonBuilder()
										.WithEmote(ticketEmoji)
										.WithLabel("Vote to Cancel")
										.WithCustomId($"cancel{ticket.ID}")
										.WithStyle(ButtonStyle.Danger);
			var componentBuilder = new ComponentBuilder()
										.WithButton(buttonBuilder);

			embedBuilder.WithColor(Color.Red)
						.WithTitle($"{Context.User.GetFullUsername()} Submitted a Ticket to Cancel {targetUser.GetFullUsername()}")
						.WithDescription("If this request gets at least six (6) votes in favor of cancelling, the target user will be cancelled.")
						.WithFooter($"{Context.User.GetFullUsername()}'s Cancel Ticket Request handled by Wokebucks")
						.WithUrl("https://github.com/chicklightning/WokebucksBot")
						.WithCurrentTimestamp();

			await ReplyAsync("", embed: embedBuilder.Build(), components: componentBuilder.Build());
		}

		[Command("tickets")]
		[Summary("See all of your current tickets.")]
		public async Task GetTicketsAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "tickets", Context.User.GetFullUsername());

			UserData user = await _documentClient.GetDocumentAsync<UserData>(Context.User.Id.ToString()) ?? new UserData(Context.User);

			var embedBuilder = new EmbedBuilder();
			if (user.CreatedTickets.Count == 0)
            {
				embedBuilder.WithColor(Color.Teal);
				embedBuilder.WithTitle($"Cancel Tickets Opened by {Context.User.GetFullUsername()}");
				embedBuilder.WithDescription("You have not opened any cancel tickets.");
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Cancel Tickets provided by Wokebucks");
				embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
				embedBuilder.WithCurrentTimestamp();

				await ReplyAsync("", false, embed: embedBuilder.Build());
			}

			// Return multiple options for user to select, along with bet amount
			var menuBuilder = new SelectMenuBuilder()
									.WithPlaceholder("Select a ticket you would like to view.")
									.WithCustomId("cancel");

			foreach (var ticketPair in user.CreatedTickets)
			{
				// Display option, underlying value is combined bet ID and option ID
				menuBuilder.AddOption(ticketPair.Value, ticketPair.Key);
			}

			var builder = new ComponentBuilder()
				.WithSelectMenu(menuBuilder);

			embedBuilder.WithColor(Color.Teal);
			embedBuilder.WithTitle($"Cancel Tickets Opened by {Context.User.GetFullUsername()}");
			embedBuilder.WithDescription("Select an option below to view more information on a cancel ticket.");
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Cancel Tickets provided by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
			embedBuilder.WithCurrentTimestamp();

			await ReplyAsync("", embed: embedBuilder.Build(), components: builder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully completed by user <{{{UserIdKey}}}>.", "tickets", Context.User.GetFullUsername());
		}

		[Command("complaints")]
		[Summary("See all current tickets against you.")]
		public async Task GetComplaintsAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "complaints", Context.User.GetFullUsername());

			UserData user = await _documentClient.GetDocumentAsync<UserData>(Context.User.Id.ToString()) ?? new UserData(Context.User);

			var embedBuilder = new EmbedBuilder();
			if (user.CancelTickets.Count == 0)
			{
				embedBuilder.WithColor(Color.Teal);
				embedBuilder.WithTitle($"Cancel Tickets Opened on {Context.User.GetFullUsername()}");
				embedBuilder.WithDescription("Nobody has opened any cancel tickets on you. That's pretty woke!");
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Cancel Tickets provided by Wokebucks");
				embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
				embedBuilder.WithCurrentTimestamp();

				await ReplyAsync("", false, embed: embedBuilder.Build());
			}

			// Return multiple options for user to select, along with bet amount
			var menuBuilder = new SelectMenuBuilder()
									.WithPlaceholder("Select a ticket you would like to view.")
									.WithCustomId("cancel");

			foreach (var ticketPair in user.CancelTickets)
			{
				// Display option, underlying value is combined bet ID and option ID
				menuBuilder.AddOption(ticketPair.Value, ticketPair.Key);
			}

			var builder = new ComponentBuilder()
				.WithSelectMenu(menuBuilder);

			embedBuilder.WithColor(Color.Teal);
			embedBuilder.WithTitle($"Cancel Tickets Opened on {Context.User.GetFullUsername()}");
			embedBuilder.WithDescription("Select an option below to view more information on a cancel ticket.");
			embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Cancel Tickets provided by Wokebucks");
			embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
			embedBuilder.WithCurrentTimestamp();

			await ReplyAsync("", embed: embedBuilder.Build(), components: builder.Build());

			_logger.LogInformation($"<{{{CommandName}}}> command successfully completed by user <{{{UserIdKey}}}>.", "complaints", Context.User.GetFullUsername());
		}

		private async Task<bool> ReactIfSelfWhereNotAllowedAsync(IApplication application, SocketUser target, SocketUserMessage userMessage)
		{
			if (Context.User.Id != application.Owner.Id && string.Equals(Context.User.GetFullUsername(), target.GetFullUsername()))
			{
				var embedBuilder = new EmbedBuilder();
				embedBuilder.WithColor(Color.Red);
				embedBuilder.WithTitle("Invalid Bank Transaction");
				embedBuilder.WithDescription("You can't cancel yourself ~~dumbass~~ it doesn't work like that.");
				embedBuilder.WithFooter($"{Context.User.GetFullUsername()}'s Message provided by Wokebucks");
				embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");
				embedBuilder.WithCurrentTimestamp();

				await ReplyAsync("", false, embed: embedBuilder.Build());

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