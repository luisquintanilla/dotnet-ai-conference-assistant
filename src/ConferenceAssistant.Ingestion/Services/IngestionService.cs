using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using ConferenceAssistant.Ingestion.Models;

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

    public async Task<int> IngestOutlineAsync(string markdownPath)
    {
        // 1. Reader — built-in Markdown reader from M.E.DataIngestion.Markdig
        IngestionDocumentReader reader = new MarkdownReader();

        // 2. Chunker — header-based splitting with token limits
        var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
        var chunkerOptions = new IngestionChunkerOptions(tokenizer)
        {
            MaxTokensPerChunk = 500,
            OverlapTokens = 50
        };
        IngestionChunker<string> chunker = new HeaderChunker(chunkerOptions);

        // 3. Writer — stores chunks in the vector store with auto-generated embeddings
        using var writer = new VectorStoreWriter<string>(
            _searchService.VectorStore,
            dimensionCount: 1536,
            new VectorStoreWriterOptions { CollectionName = "conference-knowledge" });

        // 4. Enrichers — AI-powered summary and keyword extraction
        var enricherOptions = new EnricherOptions(_chatClient) { LoggerFactory = _loggerFactory };
        var summaryEnricher = new SummaryEnricher(enricherOptions);
        string[] keywords = [".NET", "AI", "Microsoft.Extensions.AI", "DataIngestion", "VectorData", "MCP", "Agents", "Aspire", "Copilot", "LLM", "embeddings"];
        var keywordEnricher = new KeywordEnricher(enricherOptions, keywords);

        // 5. Pipeline — compose reader → chunker → enrichers → writer
        using IngestionPipeline<string> pipeline = new(reader, chunker, writer, new IngestionPipelineOptions(), _loggerFactory)
        {
            ChunkProcessors = { summaryEnricher, keywordEnricher }
        };

        // 6. Process the markdown file
        int count = 0;
        var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(markdownPath))!);
        var filename = Path.GetFileName(markdownPath);
        await foreach (var result in pipeline.ProcessAsync(dir, filename))
        {
            if (result.Succeeded)
            {
                count++;
                _logger.LogInformation("Ingested document {DocId} via pipeline", result.DocumentId);
            }
            else
            {
                _logger.LogWarning("Failed to process document {DocId}", result.DocumentId);
            }
        }
        return count;
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
            Id = $"response-{pollId}",
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
}
