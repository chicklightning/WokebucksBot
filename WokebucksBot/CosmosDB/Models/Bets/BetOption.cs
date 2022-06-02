using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class BetOption : IEquatable<BetOption>
    {
        [JsonProperty(PropertyName = "optId", Required = Required.Always)]
        public string OptionId { get; set; }

        [JsonProperty(PropertyName = "total", Required = Required.Always)]
        public double OptionTotal { get; set; }

        [JsonProperty(PropertyName = "voters", Required = Required.Always)]
        public HashSet<string> Voters { get; set; }

        public bool Equals(BetOption? other)
        {
            return (string.Equals(OptionId, other?.OptionId));
        }

        public override bool Equals(object? obj)
        {
            return obj is not null && obj is BetOption && Equals(obj as BetOption);
        }

        public override int GetHashCode()
        {
            return OptionId.GetHashCode();
        }
    }
}
