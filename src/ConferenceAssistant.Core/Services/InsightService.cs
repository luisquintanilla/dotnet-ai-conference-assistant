using System.Collections.Concurrent;
using ConferenceAssistant.Core.Models;

namespace ConferenceAssistant.Core.Services;

public class InsightService : IInsightService
{
    private readonly ConcurrentBag<Insight> _insights = [];

    public event Action<Insight>? InsightGenerated;

    public Task<Insight> AddInsightAsync(Insight insight)
    {
        _insights.Add(insight);

        InsightGenerated?.Invoke(insight);
        return Task.FromResult(insight);
    }

    public IReadOnlyList<Insight> GetInsightsForPoll(string pollId)
    {
        return _insights
            .Where(i => i.PollId == pollId)
            .OrderByDescending(i => i.GeneratedAt)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<Insight> GetInsightsForTopic(string topicId)
    {
        return _insights
            .Where(i => i.TopicId == topicId)
            .OrderByDescending(i => i.GeneratedAt)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<Insight> GetAllInsights()
    {
        return _insights
            .OrderByDescending(i => i.GeneratedAt)
            .ToList()
            .AsReadOnly();
    }
}
