using System.Web;
using cloud_dictionary.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;

namespace cloud_dictionary
{
    public class DefinitionOfTheDayRepository
    {
         private readonly Container _definitionOfTheDayCollection;
        private static readonly Random random = new();
        public DefinitionOfTheDayRepository(CosmosClient client, IConfiguration configuration)
        {
            var database = client.GetDatabase(configuration["AZURE_COSMOS_DATABASE_NAME"]);
            _definitionOfTheDayCollection = database.GetContainer(configuration["AZURE_COSMOS_DEFINITION_OF_THE_DAY_CONTAINER_NAME"]);
        }
        public async Task<Definition?> GetDefinitionOfTheDay()
        {
            var query = _definitionOfTheDayCollection.GetItemLinqQueryable<Definition>().Take(1).ToFeedIterator();
            if (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                return response.FirstOrDefault();
            }
            return null;
        }
        public async Task UpdateDefinitionOfTheDay(Definition newDefinition)
        {
            var query = _definitionOfTheDayCollection.GetItemLinqQueryable<Definition>().Take(1).ToFeedIterator();
            Definition currentDefinition = null;
            if (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                currentDefinition = response.FirstOrDefault();
            }
            if (currentDefinition != null)
            {
                await _definitionOfTheDayCollection.DeleteItemAsync<Definition>(currentDefinition.Id, new PartitionKey(currentDefinition.Id));
            }
            await _definitionOfTheDayCollection.UpsertItemAsync(newDefinition, new PartitionKey(newDefinition.Id));
        }

        public async Task<Definition?> GetRandomDefinitionAsync()
        {
            int count = await GetDefinitionCountAsync();
            int randomIndex = random.Next(0, count);
            var query = _definitionsCollection.GetItemLinqQueryable<Definition>()
                .Skip(randomIndex)
                .Take(1)
                .ToFeedIterator();

            List<Definition> definitions = new();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                definitions.AddRange(response.ToList());
            }
            return definitions.FirstOrDefault();
        }
    }
}