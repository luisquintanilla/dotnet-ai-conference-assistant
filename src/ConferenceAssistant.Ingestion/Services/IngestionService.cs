using System.Text;
using Microsoft.Extensions.AI;
using ConferenceAssistant.Ingestion.Models;

namespace ConferenceAssistant.Ingestion.Services;

public class IngestionService : IIngestionService
{
    private readonly ISemanticSearchService _searchService;
    private readonly IChatClient _chatClient;

    public IngestionService(ISemanticSearchService searchService, IChatClient chatClient)
    {
        _searchService = searchService;
        _chatClient = chatClient;
    }

    public async Task<int> IngestOutlineAsync(string markdownPath)
    {
        var markdown = await File.ReadAllTextAsync(markdownPath);
        var chunks = SplitMarkdownByHeadings(markdown);
        var records = new List<ConferenceRecord>();

        foreach (var (heading, body) in chunks)
        {
            if (string.IsNullOrWhiteSpace(body))
                continue;

            var content = string.IsNullOrEmpty(heading) ? body : $"{heading}\n{body}";
            var summary = await GenerateSummaryAsync(content);
            var keywords = await ExtractKeywordsAsync(content);

            records.Add(new ConferenceRecord
            {
                Source = "outline",
                TopicId = NormalizeTopicId(heading),
                Content = content,
                Summary = summary,
                Keywords = keywords
            });
        }

        await _searchService.UpsertBatchAsync(records);
        return records.Count;
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

        var content = sb.ToString();
        var summary = await GenerateSummaryAsync(content);

        var record = new ConferenceRecord
        {
            Id = $"response-{pollId}",
            Source = "response",
            TopicId = topicId,
            Content = content,
            Summary = summary
        };

        await _searchService.UpsertAsync(record);
        return 1;
    }

    public async Task<int> IngestInsightAsync(string topicId, string insightContent)
    {
        var summary = await GenerateSummaryAsync(insightContent);
        var keywords = await ExtractKeywordsAsync(insightContent);

        var record = new ConferenceRecord
        {
            Source = "insight",
            TopicId = topicId,
            Content = insightContent,
            Summary = summary,
            Keywords = keywords
        };

        await _searchService.UpsertAsync(record);
        return 1;
    }

    public async Task<int> IngestExternalContentAsync(string source, string content)
    {
        var summary = await GenerateSummaryAsync(content);
        var keywords = await ExtractKeywordsAsync(content);

        var record = new ConferenceRecord
        {
            Source = source,
            Content = content,
            Summary = summary,
            Keywords = keywords
        };

        await _searchService.UpsertAsync(record);
        return 1;
    }

    private async Task<string> GenerateSummaryAsync(string content)
    {
        var response = await _chatClient.GetResponseAsync(
            $"Summarize this content in 1-2 sentences:\n\n{content}");
        return response.Text ?? string.Empty;
    }

    private async Task<List<string>> ExtractKeywordsAsync(string content)
    {
        var response = await _chatClient.GetResponseAsync(
            $"Extract 3-5 keywords from this content. Return only the keywords separated by commas, nothing else:\n\n{content}");
        var text = response.Text ?? string.Empty;
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// Splits markdown content by ## and ### headings into (heading, body) chunks.
    /// </summary>
    private static List<(string Heading, string Body)> SplitMarkdownByHeadings(string markdown)
    {
        var chunks = new List<(string Heading, string Body)>();
        var lines = markdown.Split('\n');
        string currentHeading = "";
        var currentBody = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("## ") || trimmed.StartsWith("### "))
            {
                // Save the previous chunk
                if (currentBody.Length > 0 || !string.IsNullOrEmpty(currentHeading))
                {
                    chunks.Add((currentHeading, currentBody.ToString().Trim()));
                }

                currentHeading = trimmed.TrimStart('#', ' ');
                currentBody.Clear();
            }
            else
            {
                currentBody.AppendLine(line);
            }
        }

        // Add the last chunk
        if (currentBody.Length > 0 || !string.IsNullOrEmpty(currentHeading))
        {
            chunks.Add((currentHeading, currentBody.ToString().Trim()));
        }

        return chunks;
    }

    private static string NormalizeTopicId(string heading)
    {
        if (string.IsNullOrWhiteSpace(heading))
            return "general";

        return heading
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace("--", "-")
            .Trim('-');
    }
}
