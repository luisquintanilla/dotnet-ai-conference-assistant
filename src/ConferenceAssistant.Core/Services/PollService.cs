using System.Collections.Concurrent;
using ConferenceAssistant.Core.Models;

namespace ConferenceAssistant.Core.Services;

public class PollService : IPollService
{
    private readonly ConcurrentDictionary<string, Poll> _polls = new();
    private readonly ConcurrentDictionary<string, List<PollResponse>> _responses = new();

    public event Action<Poll>? PollActivated;
    public event Action<Poll>? PollClosed;
    public event Action<PollResponse>? ResponseReceived;

    public Task<Poll> CreatePollAsync(string topicId, string question, List<string> options, PollSource source = PollSource.Generated)
    {
        var poll = new Poll
        {
            TopicId = topicId,
            Question = question,
            Options = options,
            Source = source,
            Status = PollStatus.Draft
        };

        _polls[poll.Id] = poll;
        _responses[poll.Id] = [];

        return Task.FromResult(poll);
    }

    public Task ActivatePollAsync(string pollId)
    {
        var poll = GetPollOrThrow(pollId);
        poll.Status = PollStatus.Active;

        PollActivated?.Invoke(poll);
        return Task.CompletedTask;
    }

    public Task ClosePollAsync(string pollId)
    {
        var poll = GetPollOrThrow(pollId);
        poll.Status = PollStatus.Closed;
        poll.ClosedAt = DateTimeOffset.UtcNow;

        PollClosed?.Invoke(poll);
        return Task.CompletedTask;
    }

    public Task<PollResponse> SubmitResponseAsync(string pollId, string selectedOption, string? attendeeId = null)
    {
        _ = GetPollOrThrow(pollId);

        var response = new PollResponse
        {
            PollId = pollId,
            SelectedOption = selectedOption,
            AttendeeId = attendeeId
        };

        _responses.GetOrAdd(pollId, _ => []).Add(response);

        ResponseReceived?.Invoke(response);
        return Task.FromResult(response);
    }

    public Poll? GetActivePoll()
    {
        return _polls.Values.FirstOrDefault(p => p.Status == PollStatus.Active);
    }

    public IReadOnlyList<Poll> GetPollsForTopic(string topicId)
    {
        return _polls.Values
            .Where(p => p.TopicId == topicId)
            .OrderBy(p => p.CreatedAt)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<PollResponse> GetResponsesForPoll(string pollId)
    {
        return _responses.TryGetValue(pollId, out var responses)
            ? responses.ToList().AsReadOnly()
            : Array.Empty<PollResponse>().AsReadOnly();
    }

    public Dictionary<string, int> GetPollResults(string pollId)
    {
        if (!_responses.TryGetValue(pollId, out var responses))
        {
            return new Dictionary<string, int>();
        }

        return responses
            .GroupBy(r => r.SelectedOption)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private Poll GetPollOrThrow(string pollId)
    {
        return _polls.TryGetValue(pollId, out var poll)
            ? poll
            : throw new ArgumentException($"Poll '{pollId}' not found.");
    }
}
