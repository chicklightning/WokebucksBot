using Discord;
using Discord.WebSocket;

namespace Swamp.WokebucksBot.Bot.Extensions
{
    public static class IUserExtensions
    {
        public static string GetFullUsername(this IUser socketUser)
        {
            return $"{socketUser.Username}#{socketUser.Discriminator}";
        }
    }
}
