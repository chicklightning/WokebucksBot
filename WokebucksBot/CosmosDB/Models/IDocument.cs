using Newtonsoft.Json;

namespace Swamp.WokebucksBot.CosmosDB
{
    public abstract class IDocument
    {
        [JsonProperty(PropertyName = "id", Required = Required.Always)]
        public string ID { get; private set; }

        public IDocument(string id)
        {
            ID = id;
        }

        protected IDocument()
        {
            ID = string.Empty;
        }
    }
}
