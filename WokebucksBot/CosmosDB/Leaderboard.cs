using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class Leaderboard : IDocument
    {
        [JsonProperty(PropertyName = "ldrbrd", Required = Required.Always)]
        public IDictionary<string, double> AllUsers { get; set; }

        [JsonProperty(PropertyName = "topThree", Required = Required.Always)]
        public IDictionary<string, double> TopThreeWokest { get; set; }

        [JsonProperty(PropertyName = "sktrbrd", Required = Required.Always)]
        public IDictionary<string, double> BottomThreeWokest { get; set; }

        public Leaderboard() : base("leaderboard")
        {
            AllUsers = new Dictionary<string, double>();
            TopThreeWokest = new Dictionary<string, double>();
            BottomThreeWokest = new Dictionary<string, double>();
        }

        public void UpdateLeaderboard(string username, double balance)
        {
            AllUsers[username] = balance;
            TopThreeWokest = AllUsers
                             .OrderByDescending(x => x.Value)
                             .Take(3)
                             .ToDictionary(x => x.Key, x => x.Value);

            BottomThreeWokest = AllUsers
                             .OrderBy(x => x.Value)
                             .Take(3)
                             .ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
