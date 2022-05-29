using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class UserData : IDocument
    {
        [JsonProperty(PropertyName = "balance", Required = Required.Always)]
        public long Balance { get; set; }

        [JsonProperty(PropertyName = "lastAccess", Required = Required.Always)]
        public IDictionary<string, DateTimeOffset> LastAccessTimes { get; set; }

        public UserData(string id) : base(id)
        {
            Balance = 0;
            LastAccessTimes = new Dictionary<string, DateTimeOffset>();
        }

        public void AddDollarToBalance()
        {
            Balance += 1;
        }

        public void AddTenDollarsToBalance()
        {
            Balance += 10;
        }

        public void SubtractDollarFromBalance()
        {
            Balance -= 1;
        }

        public void SubtractTenDollarsFromBalance()
        {
            Balance -= 10;
        }

        /// <summary>
        /// Returns Double.MaxValue if there is no recorded interaction between these users.
        /// </summary>
        public double GetMinutesSinceLastUserInteractionTime(string otherUserId)
        {
            if (LastAccessTimes.ContainsKey(otherUserId))
            {
                return (DateTimeOffset.UtcNow - LastAccessTimes[otherUserId]).TotalMinutes;
            }

            return Double.MaxValue;
        }

        public void UpdateMostRecentInteractionForUser(string otherUserId)
        {
            LastAccessTimes[otherUserId] = DateTimeOffset.UtcNow;
        }

        public bool IsOverdrawn() => Balance < 0;
    }
}
