using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class Lottery : IDocument
    {
        [JsonProperty(PropertyName = "guildId", Required = Required.Always)]
        public string GuildId { get; private set; }

        [JsonProperty(PropertyName = "rec", Required = Required.Always)]
        public IDictionary<string, int> TicketsPurchased { get; set; } = new Dictionary<string, int>();

        [JsonProperty(PropertyName = "jackpot", Required = Required.Always)]
        public double JackpotAmount { get; set; } = 5;

        [JsonProperty(PropertyName = "ttlTickets", Required = Required.Always)]
        public int TotalTicketsPurchased { get; set; } = 0;

        [JsonProperty(PropertyName = "start", Required = Required.Always)]
        public DateTimeOffset LotteryStart { get; set; } = DateTimeOffset.UtcNow;

        private static Random _random = new Random();
        private const string _lotteryIdFormat = "lottery|{0}";

        public static string FormatLotteryIdFromGuildId(string guildId) => string.Format(_lotteryIdFormat, guildId);

        public static string GetGuildIdFromLotteryId(string lotteryId) => lotteryId.Replace("lottery|", String.Empty);

        public Lottery(string guildId) : base(FormatLotteryIdFromGuildId(guildId))
        {
            GuildId = guildId;
            LotteryStart = DateTimeOffset.UtcNow;
        }

        [JsonConstructor]
        private Lottery()
        {
            GuildId = string.Empty;
            LotteryStart = DateTimeOffset.UtcNow;
        }

        public void AddTicketPurchase(string userId)
        {
            if (TicketsPurchased.ContainsKey(userId))
            {
                TicketsPurchased[userId] += 1;
            }
            else
            {
                TicketsPurchased[userId] = 1;
            }

            TotalTicketsPurchased += 1;
            JackpotAmount += 2;
        }

        public string? GetWeightedRandomTotals()
        {
            if (TotalTicketsPurchased == 0)
            {
                return null;
            }

            // totalWeight is the sum of all brokers' weight

            int randomNumber = _random.Next(0, TotalTicketsPurchased);

            KeyValuePair<string, int> selectedUser = default;
            foreach (var userAndWeight in TicketsPurchased)
            {
                if (randomNumber < userAndWeight.Value)
                {
                    selectedUser = userAndWeight;
                    break;
                }

                randomNumber -= userAndWeight.Value;
            }

            // A winner has been chosen!
            return selectedUser.Key;
        }
    }
}
