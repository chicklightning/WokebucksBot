using Newtonsoft.Json;
using System.Text;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class Bet : IDocument
    {
        // string key is the user ID
        [JsonProperty(PropertyName = "bets", Required = Required.Always)]
        public IDictionary<string, Wager> Wagers { get; set; }

        [JsonProperty(PropertyName = "reas", Required = Required.Always)]
        public string Reason { get; private set; }

        [JsonProperty(PropertyName = "owner", Required = Required.Always)]
        public string OwnerId { get; private set; }

        // string key is the option name
        [JsonProperty(PropertyName = "options", Required = Required.Always)]
        public IDictionary<string, BetOption> OptionTotals { get; private set; }

        public Bet(string reason, string ownerId) : base(CreateDeterministicGUIDFromReason(reason.Trim().ToLowerInvariant()))
        {
            Wagers = new Dictionary<string, Wager>();
            OptionTotals = new Dictionary<string, BetOption>();
            Reason = reason.Trim().ToLowerInvariant();
            OwnerId = ownerId;
        }

        public void AddOptions(IEnumerable<string> options)
        {
            foreach (string option in options)
            {
                if (string.IsNullOrWhiteSpace(option))
                {
                    throw new ArgumentNullException("Cannot provide an empty option.");
                }

                var reducedOption = option.Length > 200 ? option.Substring(0, 200).Trim().ToLowerInvariant() : option.Trim().ToLowerInvariant();
                var betOption = new BetOption()
                {
                    OptionId = reducedOption,
                    OptionTotal = 0,
                    Voters = new HashSet<string>()
                };
                OptionTotals.Add(reducedOption, betOption);
            }
        }

        public bool AddBet(string option, string userId, double wager)
        {
            if (!Wagers.ContainsKey(userId))
            {
                var newWager = new Wager()
                {
                    UserID = userId,
                    Amount = wager,
                    Option = option
                };

                Wagers.Add(userId, newWager);
                OptionTotals[option].OptionTotal += wager;
                OptionTotals[option].Voters.Add(userId);
                return true;
            }

            return false;
        }

        public static string CreateDeterministicGUIDFromReason(string reason)
        {
            var guid = new Guid(System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(reason.ToLowerInvariant())).Take(16).ToArray());
            return guid.ToString();
        }

        public class BetOptionKey
        {
            public string FullKey { get; private set; }

            public string BetId { get; private set; }

            public string OptionId { get; private set; }

            private const string _keyFormat = "{0}|{1}"; // First replacement is Bet ID, second is Option ID

            public BetOptionKey(string fullKey)
            {
                FullKey = fullKey;
                BetId = fullKey.Split('|')[0];
                OptionId = fullKey.Split('|')[1];
            }

            public BetOptionKey(string betId, string optionId)
            {
                BetId = betId;
                OptionId = optionId;
                FullKey = string.Format(_keyFormat, BetId, OptionId);
            }
        }
    }
}
