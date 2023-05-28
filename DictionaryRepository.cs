using cloud_dictionary.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;

namespace cloud_dictionary
{
    public class DictionaryRepository
    {
        private readonly Container _definitionsCollection;
        public DictionaryRepository(CosmosClient client, IConfiguration configuration)
        {
            var database = client.GetDatabase(configuration["AZURE_COSMOS_DATABASE_NAME"]);
            _definitionsCollection = database.GetContainer(configuration["AZURE_COSMOS_CONTAINER_NAME"]);

        }
        public async Task<IEnumerable<Definition>> GetDefinitionsAsync(int? skip, int? batchSize)
        {
            return await ToListAsync(
                _definitionsCollection.GetItemLinqQueryable<Definition>(),
                skip,
                batchSize);
        }
        public async Task<IEnumerable<WordDefinition>> GetWordsAsync(int? skip, int? batchSize)
        {
            return await ToListAsync(
                _definitionsCollection.GetItemLinqQueryable<WordDefinition>(),
                skip,
                batchSize);
        }
        public async Task<Definition?> GetDefinitionAsync(string id)
        {
            var response = await _definitionsCollection.ReadItemAsync<Definition>(id, new PartitionKey(id));
            return response?.Resource;
        }
        public async Task<Definition?> GetDefinitionByWordAsync(string word)
        {
            var queryDefinition = new QueryDefinition("SELECT * FROM Definitions d WHERE LOWER(d.word) = @word").WithParameter("@word", word.ToLower());
            var queryResultSetIterator = _definitionsCollection.GetItemQueryIterator<Definition>(queryDefinition);
            List<Definition> definitions = new List<Definition>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<Definition> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (Definition definition in currentResultSet)
                {
                    definitions.Add(definition);
                }
            }

            return definitions.FirstOrDefault(); // since 'word' is unique, there should be only one match
        }
        public async Task<List<Definition>> GetDefinitionsByTagAsync(string tag, int? skip, int? batchSize)
        {
            // Set default values for pagination
            int skipValue = skip ?? 0;
            int batchSizeValue = batchSize ?? 100;

            // Compose SQL query with OFFSET and LIMIT for pagination
            var queryDefinition = new QueryDefinition($"SELECT * FROM Definitions d WHERE LOWER(d.tag) = @tag OFFSET {skipValue} LIMIT {batchSizeValue}")
        .WithParameter("@tag", tag.ToLower());

            // Get the iterator
            var iterator = _definitionsCollection.GetItemQueryIterator<Definition>(queryDefinition);

            // Create a list and populate it with the results from the iterator
            var items = new List<Definition>();

            while (iterator.HasMoreResults)
            {
                var result = await iterator.ReadNextAsync();
                items.AddRange(result);
            }

            return items;
        }
        public async Task DeleteDefinitionAsync(string definitionId)
        {
            await _definitionsCollection.DeleteItemAsync<Definition>(definitionId, new PartitionKey(definitionId));
        }
        public async Task AddDefinitionAsync(Definition definition)
        {
            //definition.Id = Guid.NewGuid().ToString("N");
            await _definitionsCollection.CreateItemAsync(definition, new PartitionKey(definition.Id));
        }
        public async Task UpdateDefinition(Definition existingDefinition)
        {
            await _definitionsCollection.ReplaceItemAsync(existingDefinition, existingDefinition.Id, new PartitionKey(existingDefinition.Id));
        }
        public async Task<Definition?> GetRandomDefinitionAsync()
        {

            IOrderedQueryable<Definition> linqQueryable = _definitionsCollection.GetItemLinqQueryable<Definition>();
            int count = await linqQueryable.CountAsync();
            var definitions = await GetDefinitionsAsync(null, null);
            int randomIndex = new Random().Next(0, count);
            Definition definition = definitions.ElementAt(randomIndex);
            return definition;

        }
        public async Task UpdateListItem(Definition existingItem)
        {
            await _definitionsCollection.ReplaceItemAsync(existingItem, existingItem.Id, new PartitionKey(existingItem.Id));
        }
        private async Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, int? skip, int? batchSize)
        {
            if (skip != null)
            {
                queryable = queryable.Skip(skip.Value);
            }

            if (batchSize != null)
            {
                queryable = queryable.Take(batchSize.Value);
            }

            using FeedIterator<T> iterator = queryable.ToFeedIterator();
            var items = new List<T>();

            while (iterator.HasMoreResults)
            {
                foreach (var item in await iterator.ReadNextAsync())
                {
                    items.Add(item);
                }
            }

            return items;
        }

        public async Task<List<Definition>> GetDefinitionsBySearch(string term, int? skip = 0, int? batchSize = 20)
        {
            // Query in Cosmos DB is case sensitive, so we use ToLower() 
            var queryable = _definitionsCollection.GetItemLinqQueryable<Definition>()
                .Where(d => d.Word.ToLower().Contains(term.ToLower())
                            || d.Content.ToLower().Contains(term.ToLower())
                            || d.Author.Name.ToLower().Contains(term.ToLower())
                            || d.Tag.ToLower().Contains(term.ToLower())
                            || d.Abbreviation.ToLower().Contains(term.ToLower()));

            return await ToListAsync(queryable, skip, batchSize);
        }


    }
}