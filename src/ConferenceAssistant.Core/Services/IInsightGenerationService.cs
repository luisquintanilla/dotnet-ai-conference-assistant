namespace ConferenceAssistant.Core.Services;

public interface IInsightGenerationService
{
    Task GenerateTopicInsightsAsync(string topicId);
    Task GeneratePollInsightsAsync(string pollId);
}
