using Azure.Core;
using Microsoft.Azure.Cosmos;

namespace Swamp.WokebucksBot.CosmosDB
{
    public class CosmosDBClient : IDisposable
    {
        private const string UserIdKey = "UserId";

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

        public async Task<UserData?> GetUserDataAsync(string userId)
        {
            UserData userData;
            try
            {
                userData = await _container.ReadItemAsync<UserData>(userId, PartitionKey.None);
            }
            catch (Exception e)
            {
                CosmosException ex = e as CosmosException;
                if (ex is not null)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogInformation($"No data for user with ID <{{{UserIdKey}}}>.", userId);
                        return null;
                    }
                }

                _logger.LogError(e, $"Unable to retrieve data for user with ID <{{{UserIdKey}}}>.", userId);
                throw;
            }
             
            return userData;
        }

        public async Task UpsertUserDataAsync(UserData userData)
        {
            try
            {
                await _container.UpsertItemAsync(userData);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unable to upsert data for user with ID <{{{UserIdKey}}}>.", userData.ID);
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
