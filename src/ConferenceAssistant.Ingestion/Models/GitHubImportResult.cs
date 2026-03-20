using ConferenceAssistant.Ingestion.Utilities;

namespace ConferenceAssistant.Ingestion.Models;

public record GitHubImportResult(
    int RecordCount,
    IReadOnlyList<ImportedDocument> Documents,
    IReadOnlyList<string> Errors);

public record ImportedDocument(
    string FilePath,
    string Content,
    FrontMatter? FrontMatter);
