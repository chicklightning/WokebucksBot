using Discord.WebSocket;
using Newtonsoft.Json;
using System.Text;
using Swamp.WokebucksBot.Bot.Extensions;

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

        public Bet(string reason, string ownerId) : base(CreateDeterministicGUIDFromReason(reason.Trim()))
        {
            Wagers = new Dictionary<string, Wager>();
            OptionTotals = new Dictionary<string, BetOption>();
            Reason = reason.Trim();
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

                var reducedOption = option.Length > 200 ? option.Substring(0, 200).Trim() : option.Trim();
                var betOption = new BetOption()
                {
                    OptionId = reducedOption,
                    OptionTotal = 0,
                    Voters = new HashSet<string>()
                };
                OptionTotals.Add(reducedOption, betOption);
            }
        }

        public bool AddBet(string option, SocketUser user, double wager)
        {
            string userId = user.Id.ToString();
            if (!Wagers.ContainsKey(userId))
            {
                var newWager = new Wager()
                {
                    UserID = userId,
                    Amount = wager,
                    Option = option,
                    UserName = user.GetFullUsername()
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
            var guid = new Guid(System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(reason)).Take(16).ToArray());
            return guid.ToString();
        }

        public class BetOptionKey
        {
            public string FullKey { get; private set; }

            public string BetId { get; private set; }

            public string OptionId { get; private set; }

            public string GuildId { get; private set; }

            private const string _keyFormat = "{0}|{1}|{2}"; // First replacement is Bet ID, second is Option ID, third is Guild ID

            public BetOptionKey(string fullKey)
            {
                FullKey = fullKey;

                string[] splitKey = fullKey.Split('|');
                BetId = splitKey[0];
                OptionId = splitKey[1];
                GuildId = splitKey[2];
            }

            public BetOptionKey(string betId, string optionId, string guildId)
            {
                BetId = betId;
                OptionId = optionId;
                GuildId = guildId;
                FullKey = string.Format(_keyFormat, BetId, OptionId, GuildId);
            }
        }
    }
}
