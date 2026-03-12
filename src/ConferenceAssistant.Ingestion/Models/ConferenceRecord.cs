using Microsoft.Extensions.VectorData;

namespace ConferenceAssistant.Ingestion.Models;

public class ConferenceRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

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
    public DateTimeOffset IngestedAt { get; set; } = DateTimeOffset.UtcNow;

    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
