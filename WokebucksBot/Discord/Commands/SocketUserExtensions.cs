using Discord.WebSocket;

namespace Swamp.WokebucksBot.Discord.Commands
{
    public static class SocketUserExtensions
    {
        public static string GetFullUsername(this SocketUser socketUser)
        {
            return $"{socketUser.Username}#{socketUser.Discriminator}";
        }

        public static string GetFullDatabaseId(this SocketUser socketUser)
        {
            return $"{socketUser.Username}|{socketUser.Discriminator}";
        }

        public static string SwitchToUsername(string fullDatabaseId)
        {
            return fullDatabaseId.Replace('|', '#');
        }
    }
}
