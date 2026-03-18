using ConferenceAssistant.Core.Models;

namespace ConferenceAssistant.Core.Services;

public class InsightService : IInsightService
{
    private readonly ISessionManager _sessionManager;
    private readonly ISessionService _sessionService;
    private readonly HashSet<string> _wiredSessions = [];

    public event Action<Insight>? InsightGenerated;

    public InsightService(ISessionManager sessionManager, ISessionService sessionService)
    {
        _sessionManager = sessionManager;
        _sessionService = sessionService;
    }

    private SessionContext GetDefaultContext()
    {
        var code = _sessionService.CurrentSession?.SessionCode
            ?? throw new InvalidOperationException("No default session loaded.");
        var ctx = _sessionManager.GetSession(code)
            ?? throw new InvalidOperationException($"Session '{code}' not found.");
        EnsureEventsWired(ctx);
        return ctx;
    }

    private void EnsureEventsWired(SessionContext ctx)
    {
        if (_wiredSessions.Add(ctx.Session.SessionCode))
        {
            ctx.InsightGenerated += i => InsightGenerated?.Invoke(i);
        }
    }

    public Task<Insight> AddInsightAsync(Insight insight)
    {
        var result = GetDefaultContext().AddInsight(insight);
        return Task.FromResult(result);
    }

    public IReadOnlyList<Insight> GetInsightsForPoll(string pollId) => GetDefaultContext().GetInsightsForPoll(pollId);
    public IReadOnlyList<Insight> GetInsightsForTopic(string topicId) => GetDefaultContext().GetInsightsForTopic(topicId);
    public IReadOnlyList<Insight> GetAllInsights() => GetDefaultContext().GetAllInsights();
}
