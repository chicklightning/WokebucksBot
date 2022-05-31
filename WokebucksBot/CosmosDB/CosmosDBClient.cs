using Azure.Core;
using Microsoft.Azure.Cosmos;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class CosmosDBClient : IDisposable
    {
        private const string DocumentIdKey = "DocumentId";

        private readonly ILogger<CosmosDBClient> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private bool disposedValue;

        public CosmosDBClient(ILogger<CosmosDBClient> logger, string connectionString) : this(logger, new CosmosClient(connectionString))
        {
        }

        public CosmosDBClient(ILogger<CosmosDBClient> logger, string cosmosDBEndpoint, TokenCredential tokenCredential) : this(logger, new CosmosClient(cosmosDBEndpoint, tokenCredential))
        {
        }

        private CosmosDBClient(ILogger<CosmosDBClient> logger, CosmosClient client)
        {
            _logger = logger;
            _cosmosClient = client;
            _container = _cosmosClient.GetContainer("Wokebucks", "UserBalances");
        }

        public async Task<T?> GetDocumentAsync<T>(string id) where T : IDocument
        {
            T document;
            try
            {
                document = await _container.ReadItemAsync<T>(id, new PartitionKey(id));
            }
            catch (Exception e)
            {
                CosmosException ex = e as CosmosException;
                if (ex is not null)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogInformation($"No document with ID <{{{DocumentIdKey}}}>.", id);
                        return null;
                    }
                }

                _logger.LogError(e, $"Unable to retrieve document with ID <{{{DocumentIdKey}}}>.", id);
                throw;
            }
             
            return document;
        }

        public async Task UpsertDocumentAsync<T>(T data) where T : IDocument
        {
            try
            {
                await _container.UpsertItemAsync(data);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unable to upsert document with ID <{{{DocumentIdKey}}}>.", data.ID);
                throw;
            }
        }

        public async Task DeleteDocumentAsync<T>(string id) where T : IDocument
        {
            try
            {
                await _container.DeleteItemAsync<T>(id, new PartitionKey(id));
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unable to delete document with ID <{{{DocumentIdKey}}}>.", id);
                throw;
            }
        }

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
