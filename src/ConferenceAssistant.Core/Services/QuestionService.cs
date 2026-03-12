using System.Collections.Concurrent;
using ConferenceAssistant.Core.Models;

namespace ConferenceAssistant.Core.Services;

public class QuestionService : IQuestionService
{
    private readonly ConcurrentDictionary<string, AudienceQuestion> _questions = new();
    private readonly object _upvoteLock = new();

    public event Action<AudienceQuestion>? QuestionReceived;
    public event Action<AudienceQuestion>? QuestionAnswered;
    public event Action<AudienceQuestion>? QuestionUpvoted;

    public Task<AudienceQuestion> SubmitQuestionAsync(string text, string? topicId = null, string? attendeeId = null)
    {
        var question = new AudienceQuestion
        {
            Text = text,
            TopicId = topicId,
            AttendeeId = attendeeId
        };

        _questions[question.Id] = question;

        QuestionReceived?.Invoke(question);
        return Task.FromResult(question);
    }

    public Task<AudienceQuestion?> AnswerQuestionAsync(string questionId, string answer, bool isAiGenerated = false)
    {
        if (!_questions.TryGetValue(questionId, out var question))
        {
            return Task.FromResult<AudienceQuestion?>(null);
        }

        question.Answer = answer;
        question.IsAiGenerated = isAiGenerated;

        QuestionAnswered?.Invoke(question);
        return Task.FromResult<AudienceQuestion?>(question);
    }

    public Task UpvoteQuestionAsync(string questionId)
    {
        if (_questions.TryGetValue(questionId, out var question))
        {
            lock (_upvoteLock)
            {
                question.Upvotes++;
            }

            QuestionUpvoted?.Invoke(question);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<AudienceQuestion> GetQuestionsForTopic(string topicId)
    {
        return _questions.Values
            .Where(q => q.TopicId == topicId)
            .OrderByDescending(q => q.Upvotes)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<AudienceQuestion> GetTopQuestions(int count = 10)
    {
        return _questions.Values
            .OrderByDescending(q => q.Upvotes)
            .Take(count)
            .ToList()
            .AsReadOnly();
    }
}
