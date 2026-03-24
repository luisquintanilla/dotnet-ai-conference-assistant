using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ConferenceAssistant.Agents.Definitions;
using ConferenceAssistant.Agents.Tools;

namespace ConferenceAssistant.Agents.Workflows;

public class PollGenerationWorkflow(IChatClient chatClient, AgentTools tools)
{
    public async Task<string> ExecuteAsync(string topicId)
    {
        ChatClientAgent agent = new(
            chatClient,
            name: "SurveyArchitect",
            description: "Generates engaging polls for conference topics",
            instructions: AgentDefinitions.SurveyArchitectInstructions,
            tools: tools.AsToolList());

        var workflow = AgentWorkflowBuilder.BuildSequential([agent]);

        var run = await InProcessExecution.Default.RunAsync(
            workflow,
            $"Generate an engaging poll for the topic currently being discussed. The topic ID is: {topicId}. Use the available tools to understand the context and create a relevant poll.");

        foreach (var evt in run.NewEvents)
        {
            if (evt is ExecutorCompletedEvent completed && completed.Data is IEnumerable<ChatMessage> msgs)
            {
                var text = string.Join("\n", msgs
                    .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
                    .Select(m => m.Text));
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return "Unable to generate poll — no agent output collected.";
    }
}
