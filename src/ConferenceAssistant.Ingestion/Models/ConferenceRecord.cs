using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.VectorData;

namespace ConferenceAssistant.Ingestion.Models;

public class ConferenceRecord
{
    [VectorStoreKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [VectorStoreData]
    public string Source { get; set; } = "";

    [VectorStoreData]
    public string TopicId { get; set; } = "";

    [VectorStoreData]
    public string Content { get; set; } = "";

    [VectorStoreData]
    public string? Summary { get; set; }

    [VectorStoreData]
    public List<string> Keywords { get; set; } = [];

    [VectorStoreData]
    public string? Sentiment { get; set; }

    [VectorStoreData]
    public string? IngestedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    [VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float> Embedding { get; set; }

    /// <summary>
    /// Creates a deterministic GUID from a string key (for stable upsert behavior).
    /// </summary>
    public static Guid DeterministicId(string key)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}
