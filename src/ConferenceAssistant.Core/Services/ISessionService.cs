using ConferenceAssistant.Core.Models;

namespace ConferenceAssistant.Core.Services;

public interface ISessionService
{
    ConferenceSession? CurrentSession { get; }
    Task LoadSessionAsync(string seedTopicsPath);
    Task StartSessionAsync();
    Task ActivateTopicAsync(string topicId);
    Task CompleteTopicAsync(string topicId);
    Task EndSessionAsync();
    SessionTopic? GetActiveTopic();
}
