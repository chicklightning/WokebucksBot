using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class Leaderboard : IDocument
    {
        [JsonProperty(PropertyName = "topThree", Required = Required.Always)]
        public IDictionary<string, long> TopThreeWokest { get; set; }

        public Leaderboard() : base("leaderboard")
        {
            TopThreeWokest = new Dictionary<string, long>();
        }
    }
}
