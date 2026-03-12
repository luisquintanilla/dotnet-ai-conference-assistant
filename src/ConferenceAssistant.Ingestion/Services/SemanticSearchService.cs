using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using ConferenceAssistant.Ingestion.Models;

namespace ConferenceAssistant.Ingestion.Services;

public class SemanticSearchService : ISemanticSearchService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly VectorStoreCollection<string, ConferenceRecord> _collection;
    private int _recordCount;
    private bool _initialized;

    public SemanticSearchService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _embeddingGenerator = embeddingGenerator;
        var vectorStore = new InMemoryVectorStore();
        _collection = vectorStore.GetCollection<string, ConferenceRecord>("conference-knowledge");
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await _collection.EnsureCollectionExistsAsync();
            _initialized = true;
        }
    }

    public async Task<IReadOnlyList<ConferenceRecord>> SearchAsync(
        string query, int topK = 5, string? sourceFilter = null)
    {
        await EnsureInitializedAsync();

        var queryEmbedding = await _embeddingGenerator.GenerateVectorAsync(query);

        var results = _collection.SearchAsync(queryEmbedding, topK);

        var records = new List<ConferenceRecord>();
        await foreach (var result in results)
        {
            if (sourceFilter is null || result.Record.Source == sourceFilter)
            {
                records.Add(result.Record);
            }
        }

        return records;
    }

    public async Task UpsertAsync(ConferenceRecord record)
    {
        await EnsureInitializedAsync();

        if (record.Embedding.Length == 0)
        {
            record.Embedding = await _embeddingGenerator.GenerateVectorAsync(record.Content);
        }

        await _collection.UpsertAsync(record);
        Interlocked.Increment(ref _recordCount);
    }

    public async Task UpsertBatchAsync(IEnumerable<ConferenceRecord> records)
    {
        foreach (var record in records)
        {
            await UpsertAsync(record);
        }
    }

    public async Task<int> GetRecordCountAsync()
    {
        await EnsureInitializedAsync();
        return _recordCount;
    }
}
