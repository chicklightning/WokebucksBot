using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class Transaction
    {
        [JsonProperty(PropertyName = "transLog", Required = Required.Always)]
        public DateTimeOffset TimeStamp { get; set; }

        [JsonProperty(PropertyName = "init", Required = Required.Always)]
        public string TransactionInitiator { get; set; }

        [JsonProperty(PropertyName = "comm", Required = Required.Always)]
        public string Comment { get; set; }

        [JsonProperty(PropertyName = "am", Required = Required.Always)]
        public double Amount { get; set; }

        public Transaction(string transactionInitiator, double amount, string comment)
        {
            TimeStamp = DateTimeOffset.UtcNow;
            TransactionInitiator = transactionInitiator;
            Comment = comment;
            Amount = amount;
        }
    }
}
