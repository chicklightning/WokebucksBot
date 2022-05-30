﻿using Discord.Commands;
using Newtonsoft.Json;
using Swamp.WokebucksBot.Discord.Commands;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class Leaderboard : IDocument
    {
        [JsonProperty(PropertyName = "ldrbrd", Required = Required.Always)]
        public IDictionary<string, LeaderboardReference> AllUsers { get; set; }

        [JsonProperty(PropertyName = "most", Required = Required.Always)]
        public IDictionary<string, IList<LeaderboardReference>> MostWoke { get; set; }

        [JsonProperty(PropertyName = "least", Required = Required.Always)]
        public IDictionary<string, IList<LeaderboardReference>> LeastWoke { get; set; }

        public Leaderboard() : base("leaderboard")
        {
            AllUsers = new Dictionary<string, LeaderboardReference>();
            MostWoke = new Dictionary<string, IList<LeaderboardReference>>();
            LeastWoke = new Dictionary<string, IList<LeaderboardReference>>();
        }

        public void UpdateLeaderboard(SocketCommandContext context, double balance)
        {
            // Add guild if not present and update balance
            AllUsers[context.User.GetFullDatabaseId()].Balance = balance;
            AllUsers[context.User.GetFullDatabaseId()].Guilds.Add(context.Guild.Id.ToString());

            // Redo leaderboards for the guilds this user is in
            foreach (string guild in AllUsers[context.User.GetFullDatabaseId()].Guilds)
            {
                MostWoke[guild] = AllUsers
                                     .Where(userLeaderboardPair => userLeaderboardPair.Value.Guilds.Contains(guild))
                                     .OrderByDescending(userLeaderboardPair => userLeaderboardPair.Value.Balance)
                                     .Take(3)
                                     .Select(x => x.Value)
                                     .ToList();

                LeastWoke[guild] = AllUsers
                                     .Where(userLeaderboardPair => userLeaderboardPair.Value.Guilds.Contains(guild))
                                     .OrderBy(userLeaderboardPair => userLeaderboardPair.Value.Balance)
                                     .Take(3)
                                     .Select(x => x.Value)
                                     .ToList();
            }
        }
    }
}
