using ConferenceAssistant.Core.Models;

namespace ConferenceAssistant.Web.Services;

public interface ISessionPersistenceService
{
    Task SaveSessionAsync(SessionContext context);
    Task<ConferenceSession?> LoadSessionAsync(string sessionCode);
    Task SavePollAsync(string sessionId, Poll poll);
    Task SavePollResponseAsync(PollResponse response);
    Task SaveQuestionAsync(string sessionId, AudienceQuestion question);
    Task SaveQuestionAnswerAsync(string questionId, QuestionAnswer answer);
    Task SaveInsightAsync(string sessionId, Insight insight);
    Task ClearRuntimeDataAsync(string sessionId);
    Task<List<ConferenceSession>> LoadAllSessionsAsync();
}
