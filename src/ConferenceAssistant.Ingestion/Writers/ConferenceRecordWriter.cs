using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using ConferenceAssistant.Ingestion.Models;
using ConferenceAssistant.Ingestion.Services;

namespace ConferenceAssistant.Ingestion.Writers;

/// <summary>
/// Custom <see cref="IngestionChunkWriter{T}"/> that bridges the M.E.DataIngestion pipeline
/// to Qdrant via <see cref="ISemanticSearchService"/>. Receives string-content chunks from
/// the pipeline and creates <see cref="ConferenceRecord"/> entries with Guid keys.
/// </summary>
public class ConferenceRecordWriter : IngestionChunkWriter<string>
{
    private readonly ISemanticSearchService _searchService;
    private readonly string _source;
    private readonly ILogger? _logger;
    private int _count;

    /// <summary>
    /// Gets the number of records successfully written.
    /// </summary>
    public int RecordsWritten => _count;

    public ConferenceRecordWriter(
        ISemanticSearchService searchService,
        string source,
        ILogger? logger = null)
    {
        _searchService = searchService;
        _source = source;
        _logger = logger;
    }

    public override async Task WriteAsync(
        IAsyncEnumerable<IngestionChunk<string>> chunks,
        CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            var record = new ConferenceRecord
            {
                Id = ConferenceRecord.DeterministicId($"{_source}-{chunk.Document.Identifier}-{_count}"),
                Source = _source,
                Content = chunk.Content
            };

            // Transfer enrichment metadata (summaries, keywords) from the pipeline
            if (chunk.HasMetadata)
            {
                if (chunk.Metadata.TryGetValue("summary", out var summary) && summary is string s)
                    record.Summary = s;

                if (chunk.Metadata.TryGetValue("keywords", out var keywords) && keywords is IEnumerable<string> kw)
                    record.Keywords = kw.ToList();
            }

            // Add header context if available
            if (!string.IsNullOrWhiteSpace(chunk.Context))
            {
                record.Content = $"{chunk.Context}\n{record.Content}";
            }

            await _searchService.UpsertAsync(record);
            Interlocked.Increment(ref _count);

            _logger?.LogDebug(
                "Pipeline wrote chunk {Count} from {DocId} ({Source})",
                _count, chunk.Document.Identifier, _source);
        }
    }
}
