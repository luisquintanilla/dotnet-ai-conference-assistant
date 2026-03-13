using ConferenceAssistant.Core.Services;
using ConferenceAssistant.Ingestion.Services;
using ConferenceAssistant.Mcp.Clients;
using Microsoft.Extensions.AI;

namespace ConferenceAssistant.Web.Services;

public class QuestionAnsweringService(
    IChatClient chatClient,
    ISemanticSearchService searchService,
    IQuestionService questionService,
    IMcpContentClient mcpClient,
    ILogger<QuestionAnsweringService> logger) : IQuestionAnsweringService
{
    public async Task GenerateAiAnswerAsync(string questionId, string questionText, string? topicId = null)
    {
        try
        {
            // 1. Search local knowledge base
            var searchResults = await searchService.SearchAsync(questionText, topK: 5);
            var localContext = string.Join("\n\n---\n\n",
                searchResults.Select(r => r.Content).Where(c => !string.IsNullOrWhiteSpace(c)));

            // 2. Search Microsoft Learn for documentation context (via MCP)
            var docsContext = await mcpClient.SearchDocsAsync(questionText);

            // 3. Ask DeepWiki about relevant .NET repos (via MCP)
            var deepWikiContext = await mcpClient.AskDeepWikiAsync(
                "dotnet/extensions", questionText);

            // 4. Combine all context sources
            var contextParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(localContext))
                contextParts.Add($"[Session Knowledge Base]\n{localContext}");
            if (!string.IsNullOrWhiteSpace(docsContext))
                contextParts.Add($"[Microsoft Learn Documentation]\n{docsContext}");
            if (!string.IsNullOrWhiteSpace(deepWikiContext))
                contextParts.Add($"[DeepWiki - dotnet/extensions]\n{deepWikiContext}");

            var fullContext = string.Join("\n\n===\n\n", contextParts);

            var systemPrompt = """
                You are a helpful conference assistant. Answer the audience member's question using the
                provided context from the session knowledge base, Microsoft Learn docs, and GitHub wikis.
                Be concise but informative (2-4 sentences max). If the context doesn't contain enough
                information to answer well, say so honestly and provide what you can.
                Do NOT mention that you're using "context" or "documents" — just answer naturally.
                """;

            var userPrompt = string.IsNullOrWhiteSpace(fullContext)
                ? $"Question: {questionText}"
                : $"Context:\n{fullContext}\n\nQuestion: {questionText}";

            var response = await chatClient.GetResponseAsync(
            [
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            ]);

            var answer = response.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(answer))
            {
                await questionService.AnswerQuestionAsync(questionId, answer, isAiGenerated: true);
                logger.LogInformation(
                    "AI answer generated for question {QuestionId} (sources: local={Local}, docs={Docs}, wiki={Wiki})",
                    questionId,
                    !string.IsNullOrWhiteSpace(localContext),
                    !string.IsNullOrWhiteSpace(docsContext),
                    !string.IsNullOrWhiteSpace(deepWikiContext));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate AI answer for question {QuestionId}", questionId);
        }
    }
}
