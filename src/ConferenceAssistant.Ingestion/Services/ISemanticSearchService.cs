using ConferenceAssistant.Ingestion.Models;

namespace ConferenceAssistant.Ingestion.Services;

public interface ISemanticSearchService
{
    Task<IReadOnlyList<ConferenceRecord>> SearchAsync(string query, int topK = 5, string? sourceFilter = null);
    Task UpsertAsync(ConferenceRecord record);
    Task UpsertBatchAsync(IEnumerable<ConferenceRecord> records);
    Task<int> GetRecordCountAsync();
}
