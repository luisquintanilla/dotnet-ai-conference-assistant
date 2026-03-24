using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using ConferenceAssistant.Ingestion.Services;

namespace ConferenceAssistant.Agents.Tools;

public static class KnowledgeTools
{
    public static IList<AITool> CreateTools(ISemanticSearchService searchService, IIngestionService ingestionService)
    {
        return
        [
            AIFunctionFactory.Create(
                [Description("Search the conference knowledge base using semantic search to find relevant content")]
                async (
                    [Description("The search query to find relevant knowledge")] string query,
                    [Description("Maximum number of results to return")] int maxResults = 5
                ) =>
                {
                    var results = await searchService.SearchAsync(query, maxResults);
                    if (results.Count == 0) return "No relevant content found in the knowledge base.";

                    var sb = new StringBuilder();
                    sb.AppendLine($"Found {results.Count} result(s):");
                    for (var i = 0; i < results.Count; i++)
                    {
                        var r = results[i];
                        sb.AppendLine($"\n--- Result {i + 1} [{r.Source}] ---");
                        sb.AppendLine(r.Content);
                        if (!string.IsNullOrEmpty(r.Context))
                            sb.AppendLine($"Context: {r.Context}");
                    }
                    return sb.ToString();
                },
                "SearchKnowledge"),

            AIFunctionFactory.Create(
                [Description("Get statistics about the knowledge base including total record count")]
                async () =>
                {
                    var count = await searchService.GetRecordCountAsync();
                    return $"Knowledge base contains {count} record(s).";
                },
                "GetKnowledgeBaseStats"),

            AIFunctionFactory.Create(
                [Description("Ingest external content into the knowledge base for future retrieval")]
                async (
                    [Description("The source label for the content (e.g. 'external', 'speaker-notes')")] string source,
                    [Description("The content text to ingest")] string content
                ) =>
                {
                    var count = await ingestionService.IngestExternalContentAsync(source, content);
                    return $"Content ingested successfully. {count} record(s) added from source '{source}'.";
                },
                "IngestContent"),
        ];
    }
}
