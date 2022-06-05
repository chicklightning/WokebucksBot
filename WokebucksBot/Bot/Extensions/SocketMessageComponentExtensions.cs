using Discord;
using Discord.WebSocket;

namespace Swamp.WokebucksBot.Bot.Extensions
{
    public static class SocketMessageComponentExtensions
    {
        public static Task FollowupWithErrorAsync(this SocketMessageComponent messageComponent, EmbedBuilder embedBuilder, SocketUser user, string message)
        {
            embedBuilder.WithColor(Color.Red);
            embedBuilder.WithTitle("Invalid Bank Transaction");
            embedBuilder.WithDescription(message);
            embedBuilder.WithFooter($"{user.GetFullUsername()}'s Message provided by Wokebucks");
            embedBuilder.WithCurrentTimestamp();
            embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

            return messageComponent.FollowupAsync("", ephemeral: true, embed: embedBuilder.Build());
        }
    }
}
