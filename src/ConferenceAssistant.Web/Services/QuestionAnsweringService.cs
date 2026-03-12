using ConferenceAssistant.Core.Services;
using ConferenceAssistant.Ingestion.Services;
using Microsoft.Extensions.AI;

namespace ConferenceAssistant.Web.Services;

public class QuestionAnsweringService(
    IChatClient chatClient,
    ISemanticSearchService searchService,
    IQuestionService questionService,
    ILogger<QuestionAnsweringService> logger) : IQuestionAnsweringService
{
    public async Task GenerateAiAnswerAsync(string questionId, string questionText, string? topicId = null)
    {
        try
        {
            // Search the knowledge base for relevant context
            var searchResults = await searchService.SearchAsync(questionText, topK: 5);

            var contextChunks = searchResults
                .Select(r => r.Content)
                .Where(c => !string.IsNullOrWhiteSpace(c));

            var context = string.Join("\n\n---\n\n", contextChunks);

            var systemPrompt = """
                You are a helpful conference assistant. Answer the audience member's question using the 
                provided session context. Be concise but informative (2-4 sentences max). If the context 
                doesn't contain enough information to answer well, say so honestly and provide what you can.
                Do NOT mention that you're using "context" or "documents" — just answer naturally.
                """;

            var userPrompt = string.IsNullOrWhiteSpace(context)
                ? $"Question: {questionText}"
                : $"Session context:\n{context}\n\nQuestion: {questionText}";

            var response = await chatClient.GetResponseAsync(
            [
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            ]);

            var answer = response.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(answer))
            {
                await questionService.AnswerQuestionAsync(questionId, answer, isAiGenerated: true);
                logger.LogInformation("AI answer generated for question {QuestionId}", questionId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate AI answer for question {QuestionId}", questionId);
        }
    }
}
