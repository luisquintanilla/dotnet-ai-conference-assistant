using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using ConferenceAssistant.Core.Services;

namespace ConferenceAssistant.Agents.Tools;

public static class SessionTools
{
    public static IList<AITool> CreateTools(ISessionService sessionService, IQuestionService questionService)
    {
        return
        [
            AIFunctionFactory.Create(
                [Description("Get the current conference session status including active topic and overall progress")]
                () =>
                {
                    var session = sessionService.CurrentSession;
                    if (session is null) return "No session loaded.";

                    var activeTopic = sessionService.GetActiveTopic();
                    var completedCount = session.Topics.Count(t => t.Status == Core.Models.TopicStatus.Completed);
                    var sb = new StringBuilder();
                    sb.AppendLine($"Session: {session.Title}");
                    sb.AppendLine($"Status: {session.Status}");
                    sb.AppendLine($"Topics: {completedCount}/{session.Topics.Count} completed");
                    if (session.StartedAt.HasValue)
                        sb.AppendLine($"Started: {session.StartedAt.Value:u}");
                    if (activeTopic is not null)
                    {
                        sb.AppendLine($"\nActive Topic: {activeTopic.Title}");
                        sb.AppendLine($"Description: {activeTopic.Description}");
                    }
                    else
                    {
                        sb.AppendLine("\nNo active topic.");
                    }
                    return sb.ToString();
                },
                "GetSessionStatus"),

            AIFunctionFactory.Create(
                [Description("Get detailed information about a specific topic including its talking points and suggested polls")]
                (
                    [Description("The topic ID to get details for")] string topicId
                ) =>
                {
                    var session = sessionService.CurrentSession;
                    if (session is null) return "No session loaded.";

                    var topic = session.Topics.FirstOrDefault(t => t.Id == topicId);
                    if (topic is null) return $"Topic '{topicId}' not found.";

                    var sb = new StringBuilder();
                    sb.AppendLine($"Topic: {topic.Title}");
                    sb.AppendLine($"Status: {topic.Status}");
                    sb.AppendLine($"Description: {topic.Description}");
                    if (topic.TalkingPoints.Count > 0)
                    {
                        sb.AppendLine("\nTalking Points:");
                        foreach (var point in topic.TalkingPoints)
                            sb.AppendLine($"  - {point}");
                    }
                    if (topic.SuggestedPolls.Count > 0)
                    {
                        sb.AppendLine("\nSuggested Polls:");
                        foreach (var poll in topic.SuggestedPolls)
                            sb.AppendLine($"  - {poll.Question} [{string.Join(", ", poll.Options)}]");
                    }
                    return sb.ToString();
                },
                "GetTopicDetails"),

            AIFunctionFactory.Create(
                [Description("Get the top audience questions sorted by upvotes")]
                (
                    [Description("Maximum number of questions to return")] int count = 10
                ) =>
                {
                    var questions = questionService.GetTopQuestions(count);
                    if (questions.Count == 0) return "No audience questions yet.";

                    var sb = new StringBuilder();
                    sb.AppendLine($"Top {questions.Count} Audience Question(s):");
                    foreach (var q in questions)
                    {
                        sb.Append($"  - [{q.Upvotes} upvote(s)] {q.Text}");
                        if (q.Answers.Count > 0)
                        {
                            foreach (var a in q.Answers)
                            {
                                var badge = a.IsAiGenerated ? "AI" : a.AuthorLabel;
                                sb.Append($" → [{badge}]: {a.Text}");
                            }
                        }
                        sb.AppendLine();
                    }
                    return sb.ToString();
                },
                "GetAudienceQuestions"),
        ];
    }
}
