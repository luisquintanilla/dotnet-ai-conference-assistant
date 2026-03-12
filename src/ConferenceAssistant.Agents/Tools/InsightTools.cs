using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using ConferenceAssistant.Core.Models;
using ConferenceAssistant.Core.Services;

namespace ConferenceAssistant.Agents.Tools;

public static class InsightTools
{
    public static IList<AITool> CreateTools(IInsightService insightService)
    {
        return
        [
            AIFunctionFactory.Create(
                [Description("Get all AI-generated insights for a specific topic")]
                (
                    [Description("The topic ID to get insights for")] string topicId
                ) =>
                {
                    var insights = insightService.GetInsightsForTopic(topicId);
                    if (insights.Count == 0) return $"No insights found for topic '{topicId}'.";

                    var sb = new StringBuilder();
                    sb.AppendLine($"Insights for topic '{topicId}' ({insights.Count}):");
                    foreach (var i in insights)
                    {
                        sb.AppendLine($"\n[{i.Type}] (ID: {i.Id})");
                        sb.AppendLine(i.Content);
                        sb.AppendLine($"Generated: {i.GeneratedAt:u}");
                    }
                    return sb.ToString();
                },
                "GetInsightsForTopic"),

            AIFunctionFactory.Create(
                [Description("Get all AI-generated insights from the entire session")]
                () =>
                {
                    var insights = insightService.GetAllInsights();
                    if (insights.Count == 0) return "No insights generated yet.";

                    var sb = new StringBuilder();
                    sb.AppendLine($"All Insights ({insights.Count}):");
                    foreach (var i in insights)
                    {
                        sb.AppendLine($"\n[{i.Type}] (ID: {i.Id})");
                        if (!string.IsNullOrEmpty(i.TopicId))
                            sb.AppendLine($"Topic: {i.TopicId}");
                        if (!string.IsNullOrEmpty(i.PollId))
                            sb.AppendLine($"Poll: {i.PollId}");
                        sb.AppendLine(i.Content);
                        sb.AppendLine($"Generated: {i.GeneratedAt:u}");
                    }
                    return sb.ToString();
                },
                "GetAllInsights"),

            AIFunctionFactory.Create(
                [Description("Save a new AI-generated insight about the session, a topic, or a poll")]
                async (
                    [Description("The insight content text")] string content,
                    [Description("The type of insight: PollAnalysis, AudienceTrend, KnowledgeGap, TopicSummary, or SessionSummary")] string type,
                    [Description("Optional poll ID this insight relates to")] string? pollId = null,
                    [Description("Optional topic ID this insight relates to")] string? topicId = null
                ) =>
                {
                    var insightType = Enum.TryParse<InsightType>(type, ignoreCase: true, out var parsed)
                        ? parsed
                        : InsightType.PollAnalysis;

                    var insight = new Insight
                    {
                        Content = content,
                        Type = insightType,
                        PollId = pollId,
                        TopicId = topicId
                    };

                    var saved = await insightService.AddInsightAsync(insight);
                    return $"Insight saved (ID: {saved.Id}, Type: {saved.Type}).";
                },
                "SaveInsight"),
        ];
    }
}
