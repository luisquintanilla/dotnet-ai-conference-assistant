using ConferenceAssistant.Ingestion.Models;

namespace ConferenceAssistant.Web.Services;

public interface IContentImportService
{
    Task<ImportDraftResult> ImportAndDraftAsync(string repoUrl, string? sessionTitle = null);
}

public record ImportDraftResult(
    GitHubImportResult ImportResult,
    SessionDraft Draft);
