using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class Wager : IEquatable<Wager>
    {
        [JsonProperty(PropertyName = "user", Required = Required.Always)]
        public string UserID { get; set; }

        [JsonProperty(PropertyName = "wag", Required = Required.Always)]
        public double Amount { get; set; }

        [JsonProperty(PropertyName = "opt", Required = Required.Always)]
        public string Option { get; set; }

        public bool Equals(Wager? other)
        {
            return (string.Equals(UserID, other?.UserID));
        }

        public override bool Equals(object? obj)
        {
            return obj is not null && obj is Wager && Equals(obj as Wager);
        }

        public override int GetHashCode()
        {
            return UserID.GetHashCode();
        }
    }
}
