namespace ConferenceAssistant.Ingestion.Services;

public interface IIngestionService
{
    Task<int> IngestOutlineAsync(string markdownPath);
    Task<int> IngestResponseAsync(string pollId, string topicId, string question, Dictionary<string, int> results);
    Task<int> IngestInsightAsync(string topicId, string insightContent);
    Task<int> IngestExternalContentAsync(string source, string content);
}
