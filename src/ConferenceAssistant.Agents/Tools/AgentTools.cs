using System.ComponentModel;
using System.Text;
using ConferenceAssistant.Core.Models;
using ConferenceAssistant.Core.Services;
using ConferenceAssistant.Ingestion.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceAssistant.Agents.Tools;

// ---------------------------------------------------------------------------
// Poll domain — read + write tools for poll management
// ---------------------------------------------------------------------------

public static class AgentPollTools
{
    [Description("Get results for a specific poll showing vote counts per option")]
    [ExportAIFunction("GetPollResults")]
    public static string GetPollResults(
        [FromServices] IPollService pollService,
        [Description("The unique identifier of the poll")] string pollId)
    {
        var results = pollService.GetPollResults(pollId);
        if (results.Count == 0) return "No results yet.";
        var total = results.Values.Sum();
        return string.Join("\n", results.Select(kv =>
            $"- {kv.Key}: {kv.Value} votes ({(total > 0 ? 100 * kv.Value / total : 0)}%)"));
    }

    [Description("Get all poll results for every poll in the session")]
    [ExportAIFunction("GetAllPollResults")]
    public static string GetAllPollResults(
        [FromServices] IPollService pollService,
        [FromServices] ISessionService sessionService)
    {
        var session = sessionService.CurrentSession;
        if (session is null) return "No active session.";

        var sb = new StringBuilder();
        foreach (var topic in session.Topics)
        {
            var polls = pollService.GetPollsForTopic(topic.Id);
            foreach (var poll in polls)
            {
                var results = pollService.GetPollResults(poll.Id);
                var total = results.Values.Sum();
                sb.AppendLine($"## {poll.Question} (Topic: {topic.Title})");
                foreach (var kv in results)
                    sb.AppendLine($"  - {kv.Key}: {kv.Value} ({(total > 0 ? 100 * kv.Value / total : 0)}%)");

                var otherResponses = pollService.GetOtherResponses(poll.Id);
                if (otherResponses.Count > 0)
                {
                    sb.AppendLine("  \"Other\" responses:");
                    foreach (var text in otherResponses)
                        sb.AppendLine($"    - \"{text}\"");
                }

                sb.AppendLine();
            }
        }
        return sb.Length > 0 ? sb.ToString() : "No polls completed yet.";
    }

    [Description("Create a new poll for the audience to vote on. Set allowOther=true to let attendees provide free-text responses beyond the listed options.")]
    [ExportAIFunction("CreatePoll")]
    public static async Task<string> CreatePoll(
        [FromServices] IPollService pollService,
        [Description("The topic ID to associate the poll with")] string? topicId,
        [Description("The poll question")] string question,
        [Description("The list of answer options")] string[] options,
        [Description("Whether to allow free-text 'Other' responses. Defaults to true.")] bool allowOther = true)
    {
        var poll = await pollService.CreatePollAsync(topicId, question, options.ToList(), PollSource.Generated, allowOther);
        return $"Poll created with ID: {poll.Id}";
    }
}

// ---------------------------------------------------------------------------
// Insight domain — read + write tools for insight management
// ---------------------------------------------------------------------------

public static class AgentInsightTools
{
    [Description("Get all insights generated during the session")]
    [ExportAIFunction("GetAllInsights")]
    public static string GetAllInsights(
        [FromServices] IInsightService insightService)
    {
        var insights = insightService.GetAllInsights();
        if (insights.Count == 0) return "No insights generated yet.";
        return string.Join("\n\n", insights.Select(i => $"[{i.Type}] {i.Content}"));
    }

    [Description("Save an AI-generated insight about the session")]
    [ExportAIFunction("SaveInsight")]
    public static async Task<string> SaveInsight(
        [FromServices] IInsightService insightService,
        [Description("The insight content text")] string content,
        [Description("The type of insight: PollAnalysis, AudienceTrend, KnowledgeGap, TopicSummary, or SessionSummary")] string insightType,
        [Description("Optional poll ID this insight relates to")] string? pollId = null,
        [Description("Optional topic ID this insight relates to")] string? topicId = null)
    {
        var type = Enum.TryParse<InsightType>(insightType, true, out var t) ? t : InsightType.PollAnalysis;
        var insight = new Insight
        {
            Content = content,
            Type = type,
            PollId = pollId,
            TopicId = topicId
        };
        await insightService.AddInsightAsync(insight);
        return $"Insight saved: {insight.Id}";
    }
}

// ---------------------------------------------------------------------------
// Session domain — read-only session/topic queries
// ---------------------------------------------------------------------------

public static class AgentSessionTools
{
    [Description("Get the current active topic being discussed")]
    [ExportAIFunction("GetCurrentTopic")]
    public static string GetCurrentTopic(
        [FromServices] ISessionService sessionService)
    {
        var topic = sessionService.GetActiveTopic();
        if (topic is null) return "No active topic.";
        return $"Topic: {topic.Title}\nDescription: {topic.Description}\nTalking Points:\n{string.Join("\n- ", topic.TalkingPoints)}";
    }
}

// ---------------------------------------------------------------------------
// Knowledge domain — semantic search over the knowledge base
// ---------------------------------------------------------------------------

public static class AgentKnowledgeTools
{
    [Description("Search the session knowledge base for content related to the query")]
    [ExportAIFunction("SearchKnowledge")]
    public static async Task<string> SearchKnowledge(
        [FromServices] ISemanticSearchService searchService,
        [Description("The search query to find relevant content")] string query,
        [Description("Maximum number of results to return. Defaults to 5.")] int maxResults = 5)
    {
        var results = await searchService.SearchAsync(query, maxResults);
        if (results.Count == 0) return "No relevant content found.";
        return string.Join("\n\n---\n\n", results.Select(r => $"[{r.Source}] {r.Content}"));
    }
}

// ---------------------------------------------------------------------------
// Question domain — audience question queries
// ---------------------------------------------------------------------------

public static class AgentQuestionTools
{
    [Description("Get all audience questions, optionally filtered by topic")]
    [ExportAIFunction("GetAudienceQuestions")]
    public static string GetAudienceQuestions(
        [FromServices] IQuestionService questionService,
        [Description("Optional topic ID to filter questions by")] string? topicId = null)
    {
        var questions = topicId is not null
            ? questionService.GetQuestionsForTopic(topicId)
            : questionService.GetTopQuestions(20);
        if (questions.Count == 0) return "No audience questions yet.";
        return string.Join("\n", questions.Select(q => $"- [{q.Upvotes} votes] {q.Text}"));
    }
}

// ---------------------------------------------------------------------------
// No manual master context needed — the source generator auto-creates:
//   ConferenceAssistantAgentsToolContext.Default.Tools
// which collects every [ExportAIFunction] in the assembly.
// ---------------------------------------------------------------------------
