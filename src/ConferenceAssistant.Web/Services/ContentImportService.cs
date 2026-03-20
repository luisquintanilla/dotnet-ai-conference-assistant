using System.Text.RegularExpressions;
using ConferenceAssistant.Ingestion.Services;

namespace ConferenceAssistant.Web.Services;

public partial class ContentImportService(
    IIngestionService ingestionService,
    ISessionDraftingService draftingService,
    ILogger<ContentImportService> logger) : IContentImportService
{
    public async Task<ImportDraftResult> ImportAndDraftAsync(string repoUrl, string? sessionTitle = null)
    {
        var (owner, repo) = ParseRepoUrl(repoUrl);
        logger.LogInformation("Starting GitHub import for {Owner}/{Repo}", owner, repo);

        var importResult = await ingestionService.IngestGitHubRepoAsync(owner, repo);
        logger.LogInformation("Imported {Count} documents from {Owner}/{Repo} ({Errors} errors)",
            importResult.RecordCount, owner, repo, importResult.Errors.Count);

        var draft = await draftingService.DraftSessionAsync(importResult.Documents, sessionTitle);
        logger.LogInformation("AI draft generated: {TopicCount} topics for {Owner}/{Repo}",
            draft.Topics.Count, owner, repo);

        return new ImportDraftResult(importResult, draft);
    }

    private static (string Owner, string Repo) ParseRepoUrl(string repoUrl)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
            throw new ArgumentException("Repository URL cannot be empty.", nameof(repoUrl));

        var url = repoUrl.Trim().TrimEnd('/');

        // Remove .git suffix
        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            url = url[..^4];

        // Match patterns: https://github.com/owner/repo, github.com/owner/repo, owner/repo
        var match = RepoUrlPattern().Match(url);
        if (match.Success)
            return (match.Groups["owner"].Value, match.Groups["repo"].Value);

        throw new ArgumentException(
            $"Invalid GitHub repository URL: '{repoUrl}'. Expected formats: 'https://github.com/owner/repo', 'github.com/owner/repo', or 'owner/repo'.",
            nameof(repoUrl));
    }

    [GeneratedRegex(@"^(?:https?://)?(?:github\.com/)?(?<owner>[A-Za-z0-9_.\-]+)/(?<repo>[A-Za-z0-9_.\-]+)$")]
    private static partial Regex RepoUrlPattern();
}
