namespace Swamp.WokebucksBot.CosmosDB
{
    public class UserBalance
    {
        public string ID { get; set; }
        public long Balance { get; set; }
        public IDictionary<string, DateTimeOffset> LastAccessTimes { get; set; } = new Dictionary<string, DateTimeOffset>();
    }
}
