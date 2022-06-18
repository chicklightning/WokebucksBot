using Discord.WebSocket;
using Newtonsoft.Json;
using Swamp.WokebucksBot.Bot.Extensions;
using System.Security.Cryptography;
using System.Text;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class CancelTicket : IDocument
    {
        [JsonProperty(PropertyName = "votes", Required = Required.Always)]
        public HashSet<string> Votes { get; set; }

        [JsonProperty(PropertyName = "opened", Required = Required.Always)]
        public DateTimeOffset TicketOpened { get; set; }

        [JsonProperty(PropertyName = "desc", Required = Required.Always)]
        public string Description { get; set; }

        [JsonProperty(PropertyName = "init", Required = Required.Always)]
        public string Initiator { get; set; }

        [JsonProperty(PropertyName = "initUsername", Required = Required.Always)]
        public string InitiatorUsername { get; set; }

        [JsonProperty(PropertyName = "target", Required = Required.Always)]
        public string Target { get; set; }

        [JsonProperty(PropertyName = "targetUsername", Required = Required.Always)]
        public string TargetUsername { get; set; }

        [JsonProperty(PropertyName = "success", Required = Required.Always)]
        public bool Success { get; set; }

        // TODO: change to use datetime
        public static string CreateDeterministicTicketGuid(string initiatorId, string targetId)
        {
            byte[] message = Encoding.UTF8.GetBytes(initiatorId + targetId);
            using var alg = SHA512.Create();
            byte[] hashValue = alg.ComputeHash(message);
            Array.Resize(ref hashValue, 16);
            return new Guid(hashValue).ToString();
        }

        public CancelTicket(SocketUser target, SocketUser initiator, string description) : base(CreateDeterministicTicketGuid(initiator.Id.ToString(), target.Id.ToString()))
        {
            Votes = new HashSet<string>();
            TicketOpened = DateTimeOffset.UtcNow;
            Description = description;
            Initiator = initiator.Id.ToString();
            InitiatorUsername = initiator.GetFullUsername();
            Target = target.Id.ToString();
            TargetUsername = target.GetFullUsername();
        }

        [JsonConstructor]
        private CancelTicket()
        {
            Votes = new HashSet<string>();
            TicketOpened = DateTimeOffset.MinValue;
            Description = string.Empty;
            Initiator = string.Empty;
            InitiatorUsername = string.Empty;
            Target = string.Empty;
            TargetUsername = string.Empty;
        }
    }
}
