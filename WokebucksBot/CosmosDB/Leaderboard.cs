using Discord.WebSocket;
using Newtonsoft.Json;
using Swamp.WokebucksBot.Discord.Commands;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class Leaderboard : IDocument
    {
        [JsonProperty(PropertyName = "ldrbrd", Required = Required.Always)]
        public IDictionary<string, LeaderboardReference> AllUsers { get; set; }

        [JsonProperty(PropertyName = "most", Required = Required.Always)]
        public IDictionary<string, IDictionary<string, LeaderboardReference>> MostWoke { get; set; }

        [JsonProperty(PropertyName = "least", Required = Required.Always)]
        public IDictionary<string, IDictionary<string, LeaderboardReference>> LeastWoke { get; set; }

        public Leaderboard() : base("leaderboard")
        {
            AllUsers = new Dictionary<string, LeaderboardReference>();
            MostWoke = new Dictionary<string, IDictionary<string, LeaderboardReference>>();
            LeastWoke = new Dictionary<string, IDictionary<string, LeaderboardReference>>();
        }

        public void UpdateLeaderboard(string guildId, SocketUser user, double balance)
        {
            // Add user to leaderboard if they aren't there already
            string userId = user.Id.ToString();
            if (!AllUsers.ContainsKey(userId))
            {
                AllUsers[userId] = new LeaderboardReference()
                {
                    Balance = balance,
                    Guilds = new HashSet<string>() { guildId },
                    Username = user.GetFullUsername()
                };
            }
            else
            {
                // Add guild if not present and update balance
                AllUsers[userId].Balance = balance;
                AllUsers[userId].Guilds.Add(guildId);
            }

            // Redo leaderboards for the guilds this user is in
            foreach (string guild in AllUsers[userId].Guilds)
            {
                MostWoke[guild] = AllUsers
                                     .Where(userLeaderboardPair => userLeaderboardPair.Value.Guilds.Contains(guild))
                                     .OrderByDescending(userLeaderboardPair => userLeaderboardPair.Value.Balance)
                                     .Take(3)
                                     .ToDictionary(x => x.Key, x => x.Value);

                LeastWoke[guild] = AllUsers
                                     .Where(userLeaderboardPair => userLeaderboardPair.Value.Guilds.Contains(guild))
                                     .OrderBy(userLeaderboardPair => userLeaderboardPair.Value.Balance)
                                     .Take(3)
                                     .ToDictionary(x => x.Key, x => x.Value);
            }
        }

        public void ReconcileLeaderboard(string userId, double balance, string guildId)
        {
            if (!AllUsers.ContainsKey(userId))
            {
                throw new NullReferenceException($"User with id <{userId}> does not exist.");
            }
            else
            {
                // Add guild if not present and update balance
                AllUsers[userId].Balance = balance;
                AllUsers[userId].Guilds.Add(guildId);
            }

            // Redo leaderboards for the guilds this user is in
            foreach (string guild in AllUsers[userId].Guilds)
            {
                MostWoke[guild] = AllUsers
                                     .Where(userLeaderboardPair => userLeaderboardPair.Value.Guilds.Contains(guild))
                                     .OrderByDescending(userLeaderboardPair => userLeaderboardPair.Value.Balance)
                                     .Take(3)
                                     .ToDictionary(x => x.Key, x => x.Value);

                LeastWoke[guild] = AllUsers
                                     .Where(userLeaderboardPair => userLeaderboardPair.Value.Guilds.Contains(guild))
                                     .OrderBy(userLeaderboardPair => userLeaderboardPair.Value.Balance)
                                     .Take(3)
                                     .ToDictionary(x => x.Key, x => x.Value);
            }
        }
    }
}
