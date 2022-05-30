using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class LeaderboardReference : IEquatable<LeaderboardReference>
    {
        [JsonProperty(PropertyName = "user", Required = Required.Always)]
        public string Username { get; set; }

        [JsonProperty(PropertyName = "guilds", Required = Required.Always)]
        public HashSet<string> Guilds { get; set; } = new HashSet<string>();

        [JsonProperty(PropertyName = "bal", Required = Required.Always)]
        public double Balance { get; set; }

        public bool Equals(LeaderboardReference? other)
        {
            return (string.Equals(Username, other?.Username));
        }

        public override bool Equals(object? obj)
        {
            return obj is not null && obj is LeaderboardReference && Equals(obj as LeaderboardReference);
        }

        public override int GetHashCode()
        {
            return Username.GetHashCode();
        }
    }
}
