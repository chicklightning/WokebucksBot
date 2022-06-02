using Discord;
using Discord.Commands;
using Swamp.WokebucksBot.CosmosDB;
using Swamp.WokebucksBot.Bot.Extensions;

namespace Swamp.WokebucksBot.Bot.CommandModules
{
    public class LotteryModule : ModuleBase<SocketCommandContext>
    {
		private const string CommandName = "CommandName";
		private const string UserIdKey = "UserId";

		private readonly ILogger<LotteryModule> _logger;
		private readonly CosmosDBClient _documentClient;

		public LotteryModule(ILogger<LotteryModule> logger, CosmosDBClient docClient)
		{
			_logger = logger;
			_documentClient = docClient;
		}

		[Command("lottery")]
		[Summary("Provides a button to buy lottery tickets for $1 each and the total jackpot amount")]
		public async Task PurchaseLotteryTicketAsync()
		{
			_logger.LogInformation($"<{{{CommandName}}}> command invoked by user <{{{UserIdKey}}}>.", "lottery", Context.User.GetFullUsername());

			var lottery = await _documentClient.GetDocumentAsync<Lottery>($"{Lottery.FormatLotteryIdFromGuildId(Context.Guild.Id.ToString())}");
			if (lottery is null)
			{
				var e = new InvalidOperationException("Could not find lottery.");
				_logger.LogError(e, "Could not find lottery.");
				throw e;
			}

			var ticketEmoji = new Emoji("\uD83C\uDFAB");
			var buttonBuilder = new ButtonBuilder()
										.WithEmote(ticketEmoji)
										.WithLabel("Buy a ticket")
										.WithCustomId($"{Lottery.FormatLotteryIdFromGuildId(Context.Guild.Id.ToString())}")
										.WithStyle(ButtonStyle.Primary);
			var componentBuilder = new ComponentBuilder()
										.WithButton(buttonBuilder);

			var embedBuilder = new EmbedBuilder()
										.WithColor(Color.Blue)
										.WithTitle("Buy a Lottery Ticket")
										.WithDescription("Lottery tickets cost $1 each.")
										.AddField("Jackpot Total", "$" + string.Format("{0:0.00}", lottery.JackpotAmount))
										.WithFooter($"{Context.User.GetFullUsername()}'s Lottery Ticket Purchase Request handled by Wokebucks")
										.WithUrl("https://github.com/chicklightning/WokebucksBot")
										.WithCurrentTimestamp();

			await ReplyAsync("", embed: embedBuilder.Build(), components: componentBuilder.Build());
		}
	}
}
