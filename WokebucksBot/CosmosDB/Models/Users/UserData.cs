using Discord.WebSocket;
using Newtonsoft.Json;
using Swamp.WokebucksBot.Bot.Extensions;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class UserData : IDocument
    {
        [JsonProperty(PropertyName = "balance", Required = Required.Always)]
        public double Balance { get; set; }

        [JsonProperty(PropertyName = "lastAccess", Required = Required.Always)]
        public IDictionary<string, DateTimeOffset> LastAccessTimes { get; set; }

        [JsonProperty(PropertyName = "transLog", Required = Required.Always)]
        public IList<Transaction> TransactionLog { get; set; }

        [JsonProperty(PropertyName = "username", Required = Required.Always)]
        public string Username { get; set; }

        public UserData(SocketUser user) : base(user.Id.ToString())
        {
            Balance = 0;
            LastAccessTimes = new Dictionary<string, DateTimeOffset>();
            TransactionLog = new List<Transaction>();
            Username = user.GetFullUsername();
        }

        [JsonConstructor]
        private UserData()
        {
            Balance = 0;
            LastAccessTimes = new Dictionary<string, DateTimeOffset>();
            TransactionLog = new List<Transaction>();
            Username = string.Empty;
        }

        public void UpdateUsernameAndBalance(double amount, string username)
        {
            Balance += amount;
            Username = username;
        }

        public void AddToBalance(double amount)
        {
            Balance += amount;
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

        public void AddTransaction(string username, string comment, double amount)
        {
            TransactionLog.Add(new Transaction(username, amount, comment));

            if (TransactionLog.Count > 10)
            {
                TransactionLog = TransactionLog.OrderByDescending(x => x.TimeStamp).Take(10).ToList();
            }
        }

        public bool IsOverdrawn() => Balance < 0;
    }
}
