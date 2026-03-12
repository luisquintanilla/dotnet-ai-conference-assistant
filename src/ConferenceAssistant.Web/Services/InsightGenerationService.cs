using System.Text;
using ConferenceAssistant.Core.Models;
using ConferenceAssistant.Core.Services;
using ConferenceAssistant.Ingestion.Services;
using Microsoft.Extensions.AI;

namespace ConferenceAssistant.Web.Services;

public class InsightGenerationService(
    IChatClient chatClient,
    IInsightService insightService,
    IPollService pollService,
    IQuestionService questionService,
    ISessionService sessionService,
    ISemanticSearchService searchService,
    ILogger<InsightGenerationService> logger) : IInsightGenerationService
{
    public async Task GeneratePollInsightsAsync(string pollId)
    {
        try
        {
            var results = pollService.GetPollResults(pollId);
            if (results.Count == 0) return;

            var responses = pollService.GetResponsesForPoll(pollId);
            var polls = pollService.GetPollsForTopic(
                sessionService.GetActiveTopic()?.Id ?? "");
            var poll = polls.FirstOrDefault(p => p.Id == pollId);
            if (poll is null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Poll: {poll.Question}");
            var total = results.Values.Sum();
            foreach (var (option, count) in results.OrderByDescending(r => r.Value))
            {
                var pct = total > 0 ? (count * 100.0 / total).ToString("F0") : "0";
                sb.AppendLine($"  - {option}: {count} votes ({pct}%)");
            }
            sb.AppendLine($"Total responses: {total}");

            var prompt = $"""
                Analyze these live poll results from a conference session. Provide 1-2 short, 
                actionable insights about what the audience thinks. Be specific and reference 
                the actual numbers. Keep it under 3 sentences.

                {sb}
                """;

            var response = await chatClient.GetResponseAsync(
            [
                new(ChatRole.System, "You are a conference analytics assistant generating real-time insights from audience data."),
                new(ChatRole.User, prompt)
            ]);

            var content = response.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                await insightService.AddInsightAsync(new Insight
                {
                    TopicId = poll.TopicId,
                    PollId = pollId,
                    Content = content,
                    Type = InsightType.PollAnalysis
                });
                logger.LogInformation("Generated poll insight for poll {PollId}", pollId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate poll insight for {PollId}", pollId);
        }
    }

    public async Task GenerateTopicInsightsAsync(string topicId)
    {
        try
        {
            var topic = sessionService.CurrentSession?.Topics.FirstOrDefault(t => t.Id == topicId);
            if (topic is null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"# Topic: {topic.Title}");

            // Gather poll data
            var polls = pollService.GetPollsForTopic(topicId);
            if (polls.Count > 0)
            {
                sb.AppendLine("\n## Polls:");
                foreach (var poll in polls)
                {
                    var results = pollService.GetPollResults(poll.Id);
                    var total = results.Values.Sum();
                    sb.AppendLine($"\nQ: {poll.Question} ({total} responses)");
                    foreach (var (option, count) in results.OrderByDescending(r => r.Value))
                    {
                        var pct = total > 0 ? (count * 100.0 / total).ToString("F0") : "0";
                        sb.AppendLine($"  - {option}: {count} ({pct}%)");
                    }
                }
            }

            // Gather questions
            var questions = questionService.GetQuestionsForTopic(topicId);
            if (questions.Count > 0)
            {
                sb.AppendLine("\n## Audience Questions:");
                foreach (var q in questions.OrderByDescending(q => q.Upvotes).Take(10))
                {
                    sb.AppendLine($"- [{q.Upvotes} votes] {q.Text}");
                    if (!string.IsNullOrWhiteSpace(q.Answer))
                        sb.AppendLine($"  Answer: {q.Answer}");
                }
            }

            // Search knowledge base for additional context
            var kbResults = await searchService.SearchAsync($"topic {topic.Title}", topK: 3, sourceFilter: "outline");
            if (kbResults.Count > 0)
            {
                sb.AppendLine("\n## Session Context:");
                foreach (var r in kbResults)
                    sb.AppendLine(r.Summary ?? r.Content);
            }

            var prompt = $"""
                The following topic just completed in a live conference session. Summarize the key 
                audience takeaways: what interested them most (based on poll results), what gaps 
                they have (based on questions), and any trends you see. Keep it to 3-4 sentences.

                {sb}
                """;

            var response = await chatClient.GetResponseAsync(
            [
                new(ChatRole.System, "You are a conference analytics assistant. Generate a concise topic summary that captures audience sentiment and engagement."),
                new(ChatRole.User, prompt)
            ]);

            var content = response.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                await insightService.AddInsightAsync(new Insight
                {
                    TopicId = topicId,
                    Content = content,
                    Type = InsightType.TopicSummary
                });
                logger.LogInformation("Generated topic summary insight for {TopicId}", topicId);
            }

            // Also detect knowledge gaps from unanswered/highly-upvoted questions
            var gapQuestions = questions.Where(q => q.Upvotes >= 2 || string.IsNullOrWhiteSpace(q.Answer)).ToList();
            if (gapQuestions.Count > 0)
            {
                var gapPrompt = $"""
                    Based on these audience questions (sorted by upvotes), identify the top 1-2 
                    knowledge gaps the audience has. Be concise (1-2 sentences).

                    {string.Join("\n", gapQuestions.Select(q => $"[{q.Upvotes} votes] {q.Text}"))}
                    """;

                var gapResponse = await chatClient.GetResponseAsync(gapPrompt);
                var gapContent = gapResponse.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(gapContent))
                {
                    await insightService.AddInsightAsync(new Insight
                    {
                        TopicId = topicId,
                        Content = gapContent,
                        Type = InsightType.KnowledgeGap
                    });
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate topic insights for {TopicId}", topicId);
        }
    }
}
