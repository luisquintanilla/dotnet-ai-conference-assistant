using Microsoft.Extensions.AI;
using ConferenceAssistant.Agents.Definitions;
using ConferenceAssistant.Agents.Tools;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceAssistant.Agents.Workflows;

// Full poll access (read+write), read-only insights, plus session/knowledge/questions.
// IncludeTools on mixed-access classes prevents accidental inclusion of new writable tools.
// The assembly-wide ConferenceAssistantAgentsToolContext.Default.Tools
// also contains all tools if a single context is preferred.
[AIToolSource(typeof(AgentPollTools))]
[AIToolSource(typeof(AgentInsightTools), IncludeTools = [nameof(AgentInsightTools.GetAllInsights)])]
[AIToolSource(typeof(AgentSessionTools))]
[AIToolSource(typeof(AgentKnowledgeTools))]
[AIToolSource(typeof(AgentQuestionTools))]
partial class PollGenerationTools : AIToolContext { }

public class PollGenerationWorkflow(IChatClient chatClient)
{
    public async Task<string> ExecuteAsync(string topicId)
    {
        var options = new ChatOptions
        {
            Tools = [.. PollGenerationTools.Default.Tools]
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AgentDefinitions.SurveyArchitectInstructions),
            new(ChatRole.User, $"Generate an engaging poll for the topic currently being discussed. The topic ID is: {topicId}. Use the available tools to understand the context and create a relevant poll.")
        };

        var response = await chatClient.GetResponseAsync(messages, options);
        return response.Text ?? "Unable to generate poll.";
    }
}
