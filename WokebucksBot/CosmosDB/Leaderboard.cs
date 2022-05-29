using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class Leaderboard : IDocument
    {
        [JsonProperty(PropertyName = "ldrbrd", Required = Required.Always)]
        public IDictionary<string, long> AllUsers { get; set; }

        [JsonProperty(PropertyName = "topThree", Required = Required.Always)]
        public IDictionary<string, long> TopThreeWokest { get; set; }

        public Leaderboard() : base("leaderboard")
        {
            AllUsers = new Dictionary<string, long>();
            TopThreeWokest = new Dictionary<string, long>();
        }

        public void UpdateLeaderboard(string username, long balance)
        {
            AllUsers[username] = balance;
            TopThreeWokest = AllUsers
                             .OrderByDescending(x => x.Value)
                             .Take(3)
                             .ToDictionary(x => x.Key, x => x.Value);
		}
    }
}
