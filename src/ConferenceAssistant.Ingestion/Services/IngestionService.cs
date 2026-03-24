using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using ConferenceAssistant.Ingestion.Enrichers;
using ConferenceAssistant.Ingestion.Models;
using ConferenceAssistant.Ingestion.Readers;
using ConferenceAssistant.Ingestion.Utilities;

namespace ConferenceAssistant.Ingestion.Services;

public class IngestionService : IIngestionService
{
    private readonly ISemanticSearchService _searchService;
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        ISemanticSearchService searchService,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        ILogger<IngestionService> logger)
    {
        _searchService = searchService;
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public async Task<int> IngestResponseAsync(
        string pollId, string topicId, string question, Dictionary<string, int> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Poll: {question}");
        sb.AppendLine("Results:");
        var total = results.Values.Sum();
        foreach (var (option, count) in results)
        {
            var percentage = total > 0 ? (count * 100.0 / total).ToString("F1") : "0";
            sb.AppendLine($"  - {option}: {count} votes ({percentage}%)");
        }

        var record = new ConferenceRecord
        {
            Id = ConferenceRecord.DeterministicId($"response-{pollId}"),
            Source = "response",
            TopicId = topicId,
            Content = sb.ToString()
        };

        await _searchService.UpsertAsync(record);
        return 1;
    }

    public async Task<int> IngestInsightAsync(string topicId, string insightContent)
    {
        var record = new ConferenceRecord
        {
            Source = "insight",
            TopicId = topicId,
            Content = insightContent
        };

        await _searchService.UpsertAsync(record);
        return 1;
    }

    public async Task<int> IngestExternalContentAsync(string source, string content)
    {
        var record = new ConferenceRecord
        {
            Source = source,
            Content = content
        };

        await _searchService.UpsertAsync(record);
        return 1;
    }

    public async Task<int> IngestQuestionAsync(string questionId, string questionText, string? topicId = null)
    {
        var record = new ConferenceRecord
        {
            Id = ConferenceRecord.DeterministicId($"question-{questionId}"),
            Source = "question",
            TopicId = topicId ?? "",
            Content = $"Audience question: {questionText}"
        };

        await _searchService.UpsertAsync(record);
        return 1;
    }

    public async Task<int> IngestSessionSummaryAsync(string summaryContent)
    {
        var record = new ConferenceRecord
        {
            Id = ConferenceRecord.DeterministicId("session-summary"),
            Source = "session-summary",
            Content = summaryContent
        };

        await _searchService.UpsertAsync(record);
        _logger.LogInformation("Session summary ingested into knowledge base ({Length} chars)", summaryContent.Length);
        return 1;
    }

    public async Task<GitHubImportResult> IngestGitHubRepoAsync(
        string owner, string repo, string? subdirectory = null, string? branch = null)
    {
        using var httpClient = new HttpClient();
        var reader = new GitHubRepoReader(httpClient, owner, repo, subdirectory, branch,
            _loggerFactory.CreateLogger<GitHubRepoReader>());

        // DataIngestion components — chunker + AI enrichers for high-quality vector records
        var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
        var chunkerOptions = new IngestionChunkerOptions(tokenizer) { MaxTokensPerChunk = 500, OverlapTokens = 50 };
        IngestionChunker<string> chunker = new HeaderChunker(chunkerOptions);

        var enricherOptions = new EnricherOptions(_chatClient) { LoggerFactory = _loggerFactory };
        var summaryEnricher = new SummaryEnricher(enricherOptions);
        // Let the AI discover keywords from the content itself (no predefined list)
        var keywordEnricher = new KeywordEnricher(enricherOptions, ReadOnlySpan<string>.Empty);

        var documents = new List<ImportedDocument>();
        var errors = new List<string>();
        int vectorCount = 0;
        var source = $"github:{owner}/{repo}";

        await foreach (var ingestionDoc in reader.ReadAllAsync())
        {
            try
            {
                var rawContent = string.Join("\n", ingestionDoc.Sections
                    .SelectMany(s => s.Elements)
                    .OfType<IngestionDocumentParagraph>()
                    .Select(p => p.Text));

                if (string.IsNullOrWhiteSpace(rawContent)) continue;

                var parsed = MarkdownFrontMatterParser.Parse(rawContent);

                // Collect raw document for AI session drafting (independent of vector store)
                documents.Add(new ImportedDocument(ingestionDoc.Identifier, rawContent, parsed.FrontMatter));

                // Compose DataIngestion components: chunker → enrichers → manual write
                // This is the manual composition pattern (vs IngestionPipeline for local files)
                try
                {
                    IAsyncEnumerable<IngestionChunk<string>> chunks = chunker.ProcessAsync(ingestionDoc);
                    chunks = summaryEnricher.ProcessAsync(chunks);
                    chunks = keywordEnricher.ProcessAsync(chunks);

                    await foreach (var chunk in chunks)
                    {
                        var record = new ConferenceRecord
                        {
                            Id = ConferenceRecord.DeterministicId($"github-{owner}-{repo}-{vectorCount}"),
                            Source = source,
                            Content = !string.IsNullOrWhiteSpace(chunk.Context)
                                ? $"{chunk.Context}\n{chunk.Content}"
                                : chunk.Content
                        };

                        // Transfer AI enrichment metadata from pipeline
                        if (chunk.HasMetadata)
                        {
                            if (chunk.Metadata.TryGetValue("summary", out var summary) && summary is string s)
                                record.Summary = s;
                            if (chunk.Metadata.TryGetValue("keywords", out var kw) && kw is IEnumerable<string> kwList)
                                record.Keywords = kwList.ToList();
                        }

                        // Apply front matter enrichment (per-document metadata)
                        FrontMatterEnricher.EnrichRecord(record, parsed.FrontMatter);

                        await _searchService.UpsertAsync(record);
                        vectorCount++;
                    }

                    _logger.LogInformation("Ingested GitHub file: {FilePath} (chunks stored so far: {ChunkCount})",
                        ingestionDoc.Identifier, vectorCount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Vector store ingestion failed for {DocId} (document still available for drafting)",
                        ingestionDoc.Identifier);
                    errors.Add($"{ingestionDoc.Identifier}: Vector store - {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process GitHub file: {DocId}", ingestionDoc.Identifier);
                errors.Add($"{ingestionDoc.Identifier}: {ex.Message}");
            }
        }

        _logger.LogInformation(
            "GitHub import complete: {DocCount} docs fetched, {VectorCount} enriched chunks stored from {Owner}/{Repo}",
            documents.Count, vectorCount, owner, repo);
        return new GitHubImportResult(vectorCount, documents, errors);
    }
}
