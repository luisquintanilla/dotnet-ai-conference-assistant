using Microsoft.Extensions.AI;
using ConferenceAssistant.Agents.Definitions;
using ConferenceAssistant.Agents.Tools;

namespace ConferenceAssistant.Agents.Workflows;

public class SessionSummaryWorkflow(IChatClient chatClient, AgentTools tools)
{
    public async Task<string> ExecuteAsync()
    {
        var options = new ChatOptions { Tools = tools.AsToolList() };


        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AgentDefinitions.KnowledgeCuratorInstructions),
            new(ChatRole.User, """
                Generate a comprehensive summary of this entire session. Use ALL available tools:
                1. GetAllPollResults — to summarize every poll and its results
                2. GetAllInsights — to gather all generated insights
                3. GetAudienceQuestions — to see what the audience asked
                4. SearchKnowledge — to find key themes from the knowledge base

                Create a rich, narrative summary that covers:
                - Key topics discussed
                - Audience engagement highlights (poll participation, trends)
                - Knowledge gaps identified
                - Top audience questions
                - Overall session themes and takeaways

                Save the summary as a SessionSummary insight, then return it.
                """)
        };

        var response = await chatClient.GetResponseAsync(messages, options);
        return response.Text;
    }
}
