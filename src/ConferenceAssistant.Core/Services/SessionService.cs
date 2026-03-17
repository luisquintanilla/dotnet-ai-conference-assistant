using System.Text.Json;
using ConferenceAssistant.Core.Models;

namespace ConferenceAssistant.Core.Services;

public class SessionService : ISessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private ConferenceSession? _session;
    private List<Slide> _allSlides = [];
    private int _activeSlideIndex = -1;
    private readonly object _lock = new();

    public ConferenceSession? CurrentSession => _session;
    public Slide? ActiveSlide => _activeSlideIndex >= 0 && _activeSlideIndex < _allSlides.Count
        ? _allSlides[_activeSlideIndex]
        : null;
    public int ActiveSlideIndex => _activeSlideIndex;
    public int TotalSlides => _allSlides.Count;

    public event Action<string>? TopicActivated;
    public event Action<string>? TopicCompleted;
    public event Action? SessionEnded;
    public event Action<Slide>? SlideChanged;

    public async Task LoadSessionAsync(string seedTopicsPath)
    {
        var json = await File.ReadAllTextAsync(seedTopicsPath);
        var seedData = JsonSerializer.Deserialize<SeedSessionData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize seed topics.");

        _session = new ConferenceSession
        {
            Id = seedData.SessionId,
            Title = seedData.Title,
            Description = seedData.Description,
            Status = SessionStatus.Setup,
            Topics = seedData.Topics.Select(t => new SessionTopic
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Order = t.Order,
                Status = TopicStatus.Upcoming,
                TalkingPoints = t.TalkingPoints ?? [],
                SuggestedPolls = t.SuggestedPolls?.Select(sp => new SuggestedPoll
                {
                    Question = sp.Question,
                    Options = sp.Options ?? []
                }).ToList() ?? []
            }).ToList()
        };
    }

    public async Task LoadSlidesAsync(string slidesPath)
    {
        _allSlides = await SlideMarkdownParser.ParseFileAsync(slidesPath);

        // Map slides to topics on the session
        if (_session is not null)
        {
            foreach (var topic in _session.Topics)
            {
                topic.Slides = _allSlides.Where(s => s.TopicId == topic.Id).ToList();
            }
        }
    }

    public Task StartSessionAsync()
    {
        var session = GetSessionOrThrow();

        lock (_lock)
        {
            session.Status = SessionStatus.Live;
            session.StartedAt = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task ActivateTopicAsync(string topicId)
    {
        var session = GetSessionOrThrow();

        lock (_lock)
        {
            // Deactivate any currently active topic
            var currentActive = session.Topics.FirstOrDefault(t => t.Status == TopicStatus.Active);
            if (currentActive is not null)
            {
                currentActive.Status = TopicStatus.Completed;
            }

            var topic = session.Topics.FirstOrDefault(t => t.Id == topicId)
                ?? throw new ArgumentException($"Topic '{topicId}' not found.");

            topic.Status = TopicStatus.Active;
            session.ActiveTopicId = topicId;
        }

        TopicActivated?.Invoke(topicId);

        // Auto-navigate to the first slide of this topic
        var firstSlideIndex = _allSlides.FindIndex(s => s.TopicId == topicId);
        if (firstSlideIndex >= 0)
        {
            _activeSlideIndex = firstSlideIndex;
            SlideChanged?.Invoke(_allSlides[_activeSlideIndex]);
        }

        return Task.CompletedTask;
    }

    public Task CompleteTopicAsync(string topicId)
    {
        var session = GetSessionOrThrow();

        lock (_lock)
        {
            var topic = session.Topics.FirstOrDefault(t => t.Id == topicId)
                ?? throw new ArgumentException($"Topic '{topicId}' not found.");

            topic.Status = TopicStatus.Completed;

            if (session.ActiveTopicId == topicId)
            {
                session.ActiveTopicId = null;
            }
        }

        TopicCompleted?.Invoke(topicId);
        return Task.CompletedTask;
    }

    public Task EndSessionAsync()
    {
        var session = GetSessionOrThrow();

        lock (_lock)
        {
            session.Status = SessionStatus.Completed;
            session.EndedAt = DateTimeOffset.UtcNow;

            foreach (var topic in session.Topics.Where(t => t.Status == TopicStatus.Active))
            {
                topic.Status = TopicStatus.Completed;
            }

            session.ActiveTopicId = null;
        }

        SessionEnded?.Invoke();
        return Task.CompletedTask;
    }

    public SessionTopic? GetActiveTopic()
    {
        return _session?.Topics.FirstOrDefault(t => t.Status == TopicStatus.Active);
    }

    public List<Slide> GetSlidesForTopic(string topicId)
    {
        return _allSlides.Where(s => s.TopicId == topicId).ToList();
    }

    public Task AdvanceSlideAsync()
    {
        if (_allSlides.Count == 0) return Task.CompletedTask;

        var newIndex = _activeSlideIndex + 1;
        if (newIndex >= _allSlides.Count) return Task.CompletedTask;

        _activeSlideIndex = newIndex;
        SlideChanged?.Invoke(_allSlides[_activeSlideIndex]);
        return Task.CompletedTask;
    }

    public Task GoBackSlideAsync()
    {
        if (_allSlides.Count == 0) return Task.CompletedTask;

        var newIndex = _activeSlideIndex - 1;
        if (newIndex < 0) return Task.CompletedTask;

        _activeSlideIndex = newIndex;
        SlideChanged?.Invoke(_allSlides[_activeSlideIndex]);
        return Task.CompletedTask;
    }

    public Task GoToSlideAsync(string slideId)
    {
        var index = _allSlides.FindIndex(s => s.Id == slideId);
        if (index < 0) return Task.CompletedTask;

        _activeSlideIndex = index;
        SlideChanged?.Invoke(_allSlides[_activeSlideIndex]);
        return Task.CompletedTask;
    }

    public Slide? GetNextSlide()
    {
        var nextIndex = _activeSlideIndex + 1;
        return nextIndex < _allSlides.Count ? _allSlides[nextIndex] : null;
    }

    public Slide? GetPreviousSlide()
    {
        var prevIndex = _activeSlideIndex - 1;
        return prevIndex >= 0 ? _allSlides[prevIndex] : null;
    }

    private ConferenceSession GetSessionOrThrow()
    {
        return _session ?? throw new InvalidOperationException("No session loaded. Call LoadSessionAsync first.");
    }

    // Internal DTO for JSON deserialization
    private sealed class SeedSessionData
    {
        public string SessionId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<SeedTopic> Topics { get; set; } = [];
    }

    private sealed class SeedTopic
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int Order { get; set; }
        public List<string>? TalkingPoints { get; set; }
        public List<SeedSuggestedPoll>? SuggestedPolls { get; set; }
    }

    private sealed class SeedSuggestedPoll
    {
        public string Question { get; set; } = "";
        public List<string>? Options { get; set; }
    }
}
