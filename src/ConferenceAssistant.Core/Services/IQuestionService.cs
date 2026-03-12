using ConferenceAssistant.Core.Models;

namespace ConferenceAssistant.Core.Services;

public interface IQuestionService
{
    Task<AudienceQuestion> SubmitQuestionAsync(string text, string? topicId = null, string? attendeeId = null);
    Task<AudienceQuestion?> AnswerQuestionAsync(string questionId, string answer);
    Task UpvoteQuestionAsync(string questionId);
    IReadOnlyList<AudienceQuestion> GetQuestionsForTopic(string topicId);
    IReadOnlyList<AudienceQuestion> GetTopQuestions(int count = 10);
}
