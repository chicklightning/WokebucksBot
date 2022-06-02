using Discord;
using Discord.WebSocket;

namespace Swamp.WokebucksBot.Bot.Extensions
{
    public static class SocketModalExtensions
    {
        public static Task FollowUpWithErrorAsync(this SocketModal socketModal, EmbedBuilder embedBuilder, SocketUser user, string message)
        {
            embedBuilder.WithColor(Color.Red);
            embedBuilder.WithTitle("Invalid Bank Transaction");
            embedBuilder.WithDescription(message);
            embedBuilder.WithFooter($"{user.GetFullUsername()}'s Message provided by Wokebucks");
            embedBuilder.WithCurrentTimestamp();
            embedBuilder.WithUrl("https://github.com/chicklightning/WokebucksBot");

            return socketModal.FollowupAsync("", ephemeral: true, embed: embedBuilder.Build());
        }
    }
}
