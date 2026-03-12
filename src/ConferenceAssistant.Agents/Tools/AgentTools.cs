using System.ComponentModel;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.AI;
using ConferenceAssistant.Core.Models;
using ConferenceAssistant.Core.Services;
using ConferenceAssistant.Ingestion.Services;

namespace ConferenceAssistant.Agents.Tools;

public class AgentTools(
    IPollService pollService,
    IInsightService insightService,
    IQuestionService questionService,
    ISessionService sessionService,
    ISemanticSearchService searchService)
{
    /// <summary>
    /// Creates an <see cref="AITool"/> list from all [Description]-annotated methods on this instance.
    /// </summary>
    public IList<AITool> AsToolList() =>
        GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<DescriptionAttribute>() is not null)
            .Select(m => (AITool)AIFunctionFactory.Create(m, this))
            .ToList();

    [Description("Search the session knowledge base for content related to the query")]
    public async Task<string> SearchKnowledge(string query, int maxResults = 5)
    {
        var results = await searchService.SearchAsync(query, maxResults);
        if (results.Count == 0) return "No relevant content found.";
        return string.Join("\n\n---\n\n", results.Select(r => $"[{r.Source}] {r.Content}"));
    }

    [Description("Get the current active topic being discussed")]
    public string GetCurrentTopic()
    {
        var topic = sessionService.GetActiveTopic();
        if (topic is null) return "No active topic.";
        return $"Topic: {topic.Title}\nDescription: {topic.Description}\nTalking Points:\n{string.Join("\n- ", topic.TalkingPoints)}";
    }

    [Description("Get results for a specific poll showing vote counts per option")]
    public string GetPollResults(string pollId)
    {
        var results = pollService.GetPollResults(pollId);
        if (results.Count == 0) return "No results yet.";
        var total = results.Values.Sum();
        return string.Join("\n", results.Select(kv =>
            $"- {kv.Key}: {kv.Value} votes ({(total > 0 ? 100 * kv.Value / total : 0)}%)"));
    }

    [Description("Create a new poll for the audience to vote on")]
    public async Task<string> CreatePoll(string topicId, string question, string[] options)
    {
        var poll = await pollService.CreatePollAsync(topicId, question, options.ToList(), PollSource.Generated);
        return $"Poll created with ID: {poll.Id}";
    }

    [Description("Save an AI-generated insight about the session")]
    public async Task<string> SaveInsight(string content, string insightType, string? pollId = null, string? topicId = null)
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

    [Description("Get all audience questions, optionally filtered by topic")]
    public string GetAudienceQuestions(string? topicId = null)
    {
        var questions = topicId is not null
            ? questionService.GetQuestionsForTopic(topicId)
            : questionService.GetTopQuestions(20);
        if (questions.Count == 0) return "No audience questions yet.";
        return string.Join("\n", questions.Select(q => $"- [{q.Upvotes} votes] {q.Text}"));
    }

    [Description("Get all insights generated during the session")]
    public string GetAllInsights()
    {
        var insights = insightService.GetAllInsights();
        if (insights.Count == 0) return "No insights generated yet.";
        return string.Join("\n\n", insights.Select(i => $"[{i.Type}] {i.Content}"));
    }

    [Description("Get all poll results for every poll in the session")]
    public string GetAllPollResults()
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
                sb.AppendLine();
            }
        }
        return sb.Length > 0 ? sb.ToString() : "No polls completed yet.";
    }
}
