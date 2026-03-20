using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using ConferenceAssistant.Core.Models;
using ConferenceAssistant.Core.Services;

namespace ConferenceAssistant.Agents.Tools;

public static class PollTools
{
    public static IList<AITool> CreateTools(IPollService pollService)
    {
        return
        [
            AIFunctionFactory.Create(
                [Description("Create a new poll for the audience, optionally associated with a topic")]
                async (
                    [Description("The topic ID to associate the poll with (optional)")] string? topicId,
                    [Description("The poll question to ask the audience")] string question,
                    [Description("The answer options for the poll")] string[] options
                ) =>
                {
                    var poll = await pollService.CreatePollAsync(topicId, question, options.ToList(), PollSource.Generated);
                    return $"Poll created (ID: {poll.Id})\nQuestion: {poll.Question}\nOptions: {string.Join(", ", poll.Options)}\nStatus: {poll.Status}";
                },
                "CreatePoll"),

            AIFunctionFactory.Create(
                [Description("Get the voting results for a specific poll, showing vote counts and percentages per option")]
                (
                    [Description("The poll ID to get results for")] string pollId
                ) =>
                {
                    var results = pollService.GetPollResults(pollId);
                    if (results.Count == 0) return "No results yet for this poll.";

                    var total = results.Values.Sum();
                    var sb = new StringBuilder();
                    sb.AppendLine($"Poll Results (Total votes: {total}):");
                    foreach (var kv in results)
                    {
                        var pct = total > 0 ? 100 * kv.Value / total : 0;
                        sb.AppendLine($"  - {kv.Key}: {kv.Value} votes ({pct}%)");
                    }
                    return sb.ToString();
                },
                "GetPollResults"),

            AIFunctionFactory.Create(
                [Description("Get the currently active poll that the audience can vote on")]
                () =>
                {
                    var poll = pollService.GetActivePoll();
                    if (poll is null) return "No active poll at this time.";

                    var results = pollService.GetPollResults(poll.Id);
                    var total = results.Values.Sum();
                    var sb = new StringBuilder();
                    sb.AppendLine($"Active Poll (ID: {poll.Id})");
                    sb.AppendLine($"Question: {poll.Question}");
                    sb.AppendLine($"Options: {string.Join(", ", poll.Options)}");
                    sb.AppendLine($"Total responses: {total}");
                    if (total > 0)
                    {
                        sb.AppendLine("Current results:");
                        foreach (var kv in results)
                        {
                            var pct = 100 * kv.Value / total;
                            sb.AppendLine($"  - {kv.Key}: {kv.Value} votes ({pct}%)");
                        }
                    }
                    return sb.ToString();
                },
                "GetActivePoll"),

            AIFunctionFactory.Create(
                [Description("Close an active poll and return the final results")]
                async (
                    [Description("The poll ID to close")] string pollId
                ) =>
                {
                    await pollService.ClosePollAsync(pollId);
                    var results = pollService.GetPollResults(pollId);
                    var total = results.Values.Sum();
                    var sb = new StringBuilder();
                    sb.AppendLine($"Poll closed. Final results (Total votes: {total}):");
                    foreach (var kv in results)
                    {
                        var pct = total > 0 ? 100 * kv.Value / total : 0;
                        sb.AppendLine($"  - {kv.Key}: {kv.Value} votes ({pct}%)");
                    }
                    return sb.ToString();
                },
                "ClosePoll"),
        ];
    }
}
