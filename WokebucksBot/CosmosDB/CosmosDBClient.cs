using Azure.Core;
using Microsoft.Azure.Cosmos;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class CosmosDBClient : IDisposable
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private bool disposedValue;

        public CosmosDBClient(string connectionString) : this(new CosmosClient(connectionString))
        {
        }

        public CosmosDBClient(string cosmosDBEndpoint, TokenCredential tokenCredential) : this(new CosmosClient(cosmosDBEndpoint, tokenCredential))
        {
        }

        private CosmosDBClient(CosmosClient client)
        {
            _cosmosClient = client;
            _container = _cosmosClient.GetContainer("Wokebucks", "UserBalances");
        }

        public 

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _cosmosClient.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
