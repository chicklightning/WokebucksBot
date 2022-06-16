using Discord.WebSocket;
using Newtonsoft.Json;
using Swamp.WokebucksBot.Bot.Extensions;
using System.Linq;

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

        [JsonProperty(PropertyName = "lvl", Required = Required.Always)]
        public uint Level { get; set; }

        [JsonProperty(PropertyName = "cncl", Required = Required.Always)]
        public IDictionary<string, string> CancelTickets { get; set; }

        [JsonProperty(PropertyName = "tckts", Required = Required.Always)]
        public IDictionary<string, string> CreatedTickets { get; set; }

        public UserData(SocketUser user) : base(user.Id.ToString())
        {
            Balance = 0;
            LastAccessTimes = new Dictionary<string, DateTimeOffset>();
            TransactionLog = new List<Transaction>();
            Username = user.GetFullUsername();
            Level = uint.MinValue;
            CancelTickets = new Dictionary<string, string>();
            CreatedTickets = new Dictionary<string, string>();
        }

        [JsonConstructor]
        private UserData()
        {
            Balance = 0;
            LastAccessTimes = new Dictionary<string, DateTimeOffset>();
            TransactionLog = new List<Transaction>();
            Username = string.Empty;
            Level = uint.MinValue;
            CancelTickets = new Dictionary<string, string>();
            CreatedTickets = new Dictionary<string, string>();
        }

        public void UpdateUsernameAndAddToBalance(double amount, string username)
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

        public void CancelUser(string transactionInitiator, string updatedUsername)
        {
            // If the user's balance is positive, reset it to 0; if the user's balance is negative, double it
            double amount = (this.Balance >= 0) ? this.Balance * -1 : this.Balance;

            this.AddTransaction(transactionInitiator, "This person was canceled.", amount);
            this.UpdateUsernameAndAddToBalance(amount, updatedUsername);
        }

        public void AddCancelTicket(CancelTicket cancelTicket)
        {
            CancelTickets.Add(cancelTicket.ID, $"Started by {cancelTicket.InitiatorUsername} because \"{cancelTicket.Description}\".");
            if (CancelTickets.Count > 10)
            {
                CancelTickets = CancelTickets.Take(10).ToDictionary(x => x.Key, x => x.Value);
            }
        }

        public void AddCreatedTicket(CancelTicket createdTicket)
        {
            CreatedTickets.Add(createdTicket.ID, $"Started for {createdTicket.TargetUsername} because \"{createdTicket.Description}\".");
            if (CreatedTickets.Count > 10)
            {
                CreatedTickets = CreatedTickets.Take(10).ToDictionary(x => x.Key, x => x.Value);
            }
        }

        public bool IsOverdrawn() => Balance < 0;
    }
}
