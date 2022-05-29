using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public abstract class IDocument
    {
        [JsonProperty(PropertyName = "id", Required = Required.Always)]
        public string ID { get; }

        public IDocument(string id)
        {
            ID = id;
        }
    }
}
