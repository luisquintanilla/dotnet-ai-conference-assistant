using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using ConferenceAssistant.Ingestion.Enrichers;
using ConferenceAssistant.Ingestion.Models;
using ConferenceAssistant.Ingestion.Readers;
using ConferenceAssistant.Ingestion.Utilities;
using ConferenceAssistant.Ingestion.Writers;

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

        // 3. Writer — custom bridge: pipeline chunks → ConferenceRecord → Qdrant
        using var writer = new ConferenceRecordWriter(
            _searchService, "outline", _logger);

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

        // 6. Process the markdown file through the full pipeline
        int pipelineCount = 0;
        var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(markdownPath))!);
        var filename = Path.GetFileName(markdownPath);
        await foreach (var result in pipeline.ProcessAsync(dir, filename))
        {
            if (result.Succeeded)
            {
                pipelineCount++;
                _logger.LogInformation("Ingested document {DocId} via pipeline", result.DocumentId);
            }
            else
            {
                _logger.LogWarning("Failed to process document {DocId}", result.DocumentId);
            }
        }

        _logger.LogInformation(
            "Outline ingestion complete: {PipelineCount} pipeline docs, {WriterCount} enriched records stored in Qdrant",
            pipelineCount, writer.RecordsWritten);
        return writer.RecordsWritten;
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

        // Set up chunker for splitting large documents (reuses DataIngestion's HeaderChunker)
        var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
        var chunkerOptions = new IngestionChunkerOptions(tokenizer) { MaxTokensPerChunk = 500, OverlapTokens = 50 };
        IngestionChunker<string> chunker = new HeaderChunker(chunkerOptions);

        var documents = new List<ImportedDocument>();
        var errors = new List<string>();
        int vectorCount = 0;
        var source = $"github:{owner}/{repo}";

        await foreach (var ingestionDoc in reader.ReadAllAsync())
        {
            try
            {
                // Extract the raw markdown from the IngestionDocument
                var rawContent = string.Join("\n", ingestionDoc.Sections
                    .SelectMany(s => s.Elements)
                    .OfType<IngestionDocumentParagraph>()
                    .Select(p => p.Text));

                if (string.IsNullOrWhiteSpace(rawContent)) continue;

                // Parse front matter
                var parsed = MarkdownFrontMatterParser.Parse(rawContent);

                // Always collect the document for drafting (independent of vector store)
                documents.Add(new ImportedDocument(ingestionDoc.Identifier, rawContent, parsed.FrontMatter));

                // Chunk the document using DataIngestion's HeaderChunker and store each chunk
                try
                {
                    await foreach (var chunk in chunker.ProcessAsync(ingestionDoc))
                    {
                        var record = new ConferenceRecord
                        {
                            Id = ConferenceRecord.DeterministicId($"github-{owner}-{repo}-{vectorCount}"),
                            Source = source,
                            Content = !string.IsNullOrWhiteSpace(chunk.Context)
                                ? $"{chunk.Context}\n{chunk.Content}"
                                : chunk.Content
                        };

                        FrontMatterEnricher.EnrichRecord(record, parsed.FrontMatter);

                        await _searchService.UpsertAsync(record);
                        vectorCount++;
                    }

                    _logger.LogInformation("Ingested GitHub file: {FilePath} ({ChunkCount} chunks)", ingestionDoc.Identifier, vectorCount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Vector store upsert failed for {DocId} (document still available for drafting)", ingestionDoc.Identifier);
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
            "GitHub import complete: {DocCount} documents fetched, {VectorCount} chunks stored in vector DB from {Owner}/{Repo}",
            documents.Count, vectorCount, owner, repo);
        return new GitHubImportResult(vectorCount, documents, errors);
    }
}
