using ConferenceAssistant.Core.Models;

namespace ConferenceAssistant.Core.Services;

public interface IPollService
{
    event Action<Poll>? PollActivated;
    event Action<Poll>? PollClosed;
    event Action<PollResponse>? ResponseReceived;

    Task<Poll> CreatePollAsync(string? topicId, string question, List<string> options, PollSource source = PollSource.Generated);
    Task ActivatePollAsync(string pollId);
    Task ClosePollAsync(string pollId);
    Task<PollResponse> SubmitResponseAsync(string pollId, string selectedOption, string? attendeeId = null);
    Poll? GetActivePoll();
    Poll? GetPoll(string pollId);
    IReadOnlyList<Poll> GetPollsForTopic(string topicId);
    IReadOnlyList<PollResponse> GetResponsesForPoll(string pollId);
    Dictionary<string, int> GetPollResults(string pollId);
}
