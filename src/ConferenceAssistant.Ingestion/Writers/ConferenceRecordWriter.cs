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

            // Transfer enrichment metadata from all chunk processors
            if (chunk.HasMetadata)
            {
                // AI-generated summary (from SummaryEnricher)
                if (chunk.Metadata.TryGetValue("summary", out var summary) && summary is string s)
                    record.Summary = s;

                // AI-discovered keywords (from KeywordEnricher)
                if (chunk.Metadata.TryGetValue("keywords", out var keywords) && keywords is IEnumerable<string> kw)
                    record.Keywords = kw.ToList();

                // Front matter metadata (from FrontMatterChunkProcessor)
                if (chunk.Metadata.TryGetValue("front_matter_technologies", out var techs) && techs is IEnumerable<string> techList)
                {
                    foreach (var tech in techList)
                        if (!record.Keywords.Contains(tech, StringComparer.OrdinalIgnoreCase))
                            record.Keywords.Add(tech);
                }

                if (chunk.Metadata.TryGetValue("front_matter_category", out var cat) && cat is string category)
                    if (!record.Keywords.Contains(category, StringComparer.OrdinalIgnoreCase))
                        record.Keywords.Add(category);

                if (chunk.Metadata.TryGetValue("front_matter_job", out var job) && job is string jobDesc)
                    record.Summary ??= jobDesc;
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
