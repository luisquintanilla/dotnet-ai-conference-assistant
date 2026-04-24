using Microsoft.Extensions.AI;
using ConferenceAssistant.Agents.Definitions;
using ConferenceAssistant.Agents.Tools;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceAssistant.Agents.Workflows;

// Read-only polls (IncludeTools prevents new writable poll tools from leaking in),
// full insight access (read+write), plus knowledge.
// The assembly-wide ConferenceAssistantAgentsToolContext.Default.Tools
// also contains all tools if a single context is preferred.
[AIToolSource(typeof(AgentPollTools), IncludeTools = [
    nameof(AgentPollTools.GetPollResults), nameof(AgentPollTools.GetAllPollResults)])]
[AIToolSource(typeof(AgentInsightTools))]
[AIToolSource(typeof(AgentKnowledgeTools))]
partial class ResponseAnalysisTools : AIToolContext { }

public class ResponseAnalysisWorkflow(IChatClient chatClient)
{
    public async Task<string> ExecuteAsync(string pollId)
    {
        var options = new ChatOptions
        {
            Tools = [.. ResponseAnalysisTools.Default.Tools]
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AgentDefinitions.ResponseAnalystInstructions),
            new(ChatRole.User, $"Analyze the results for poll ID: {pollId}. Use the tools to get the results, find context, identify trends, and generate actionable insights.")
        };

        var response = await chatClient.GetResponseAsync(messages, options);
        return response.Text ?? "Unable to analyze poll results.";
    }
}
