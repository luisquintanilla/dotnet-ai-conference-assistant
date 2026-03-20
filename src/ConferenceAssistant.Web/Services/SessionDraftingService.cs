using System.Text;
using System.Text.Json;
using ConferenceAssistant.Ingestion.Models;
using Microsoft.Extensions.AI;

namespace ConferenceAssistant.Web.Services;

public class SessionDraftingService(
    IChatClient chatClient,
    ILogger<SessionDraftingService> logger) : ISessionDraftingService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<SessionDraft> DraftSessionAsync(IReadOnlyList<ImportedDocument> documents, string? sessionTitle = null)
    {
        if (documents.Count == 0)
        {
            return new SessionDraft
            {
                Title = sessionTitle ?? "Untitled Session",
                Description = "No documents were provided to generate a session draft."
            };
        }

        try
        {
            var systemPrompt = """
                You are a conference session designer. Given a collection of technical content documents,
                design a cohesive conference session with structured topics, talking points, and audience polls.

                Return ONLY valid JSON in this exact format:
                {
                  "title": "Session title",
                  "description": "2-3 sentence session description",
                  "topics": [
                    {
                      "title": "Topic name",
                      "description": "What this topic covers",
                      "talkingPoints": ["Point 1", "Point 2", "Point 3"],
                      "suggestedPolls": [
                        {
                          "question": "Poll question?",
                          "options": ["Option A", "Option B", "Option C", "Option D"]
                        }
                      ]
                    }
                  ]
                }

                Guidelines:
                - Create 3-6 topics that flow logically
                - Each topic should have 3-5 talking points
                - Each topic should have 1-2 suggested polls
                - Polls should engage the audience (experience level, preferences, opinions)
                - Group related content into coherent topics
                - Order topics from foundational to advanced
                """;

            var userPrompt = BuildUserPrompt(documents, sessionTitle);

            var response = await chatClient.GetResponseAsync(
            [
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            ]);

            var responseText = response.Text?.Trim();

            if (string.IsNullOrWhiteSpace(responseText))
            {
                logger.LogWarning("AI returned empty response for session drafting");
                return BuildFallbackDraft(documents, sessionTitle);
            }

            return ParseResponse(responseText, documents, sessionTitle);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate AI session draft, using fallback");
            return BuildFallbackDraft(documents, sessionTitle);
        }
    }

    private static string BuildUserPrompt(IReadOnlyList<ImportedDocument> documents, string? sessionTitle)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Here are the imported documents to base the session on:\n");

        foreach (var doc in documents)
        {
            if (doc.FrontMatter is not null)
            {
                sb.AppendLine($"## {doc.FrontMatter.Title ?? doc.FilePath}");
                if (doc.FrontMatter.Job is not null)
                    sb.AppendLine($"Job: {doc.FrontMatter.Job}");
                if (doc.FrontMatter.Category is not null)
                    sb.AppendLine($"Category: {doc.FrontMatter.Category}");
                if (doc.FrontMatter.Technologies.Count > 0)
                    sb.AppendLine($"Technologies: {string.Join(", ", doc.FrontMatter.Technologies)}");
            }
            else
            {
                sb.AppendLine($"## {doc.FilePath}");
            }

            // Include a truncated body to keep prompt reasonable
            var body = doc.Content.Length > 500 ? doc.Content[..500] + "..." : doc.Content;
            sb.AppendLine(body);
            sb.AppendLine();
        }

        if (sessionTitle is not null)
            sb.AppendLine($"\nSession title preference: {sessionTitle}");

        return sb.ToString();
    }

    private SessionDraft ParseResponse(string responseText, IReadOnlyList<ImportedDocument> documents, string? sessionTitle)
    {
        // Strip markdown code fences if present
        var json = StripCodeFences(responseText);

        try
        {
            var draft = JsonSerializer.Deserialize<SessionDraft>(json, JsonOptions);
            if (draft is not null && draft.Topics.Count > 0)
            {
                return draft;
            }

            logger.LogWarning("AI response deserialized but had no topics");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse AI response as JSON");
        }

        return BuildFallbackDraft(documents, sessionTitle);
    }

    private static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();

        // Handle ```json ... ``` or ``` ... ```
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3].TrimEnd();
            }
        }

        return trimmed;
    }

    private static SessionDraft BuildFallbackDraft(IReadOnlyList<ImportedDocument> documents, string? sessionTitle)
    {
        var topicsByCategory = new Dictionary<string, List<ImportedDocument>>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in documents)
        {
            var category = doc.FrontMatter?.Category ?? "General";
            if (!topicsByCategory.TryGetValue(category, out var list))
            {
                list = [];
                topicsByCategory[category] = list;
            }

            list.Add(doc);
        }

        var topics = topicsByCategory.Select(kvp => new TopicDraft
        {
            Title = kvp.Key,
            Description = $"Content related to {kvp.Key}",
            TalkingPoints = kvp.Value
                .Select(d => d.FrontMatter?.Title ?? d.FrontMatter?.Job ?? d.FilePath)
                .ToList(),
            SuggestedPolls = []
        }).ToList();

        return new SessionDraft
        {
            Title = sessionTitle ?? "Conference Session",
            Description = $"Session covering {topics.Count} topic(s) based on {documents.Count} imported document(s).",
            Topics = topics
        };
    }
}
