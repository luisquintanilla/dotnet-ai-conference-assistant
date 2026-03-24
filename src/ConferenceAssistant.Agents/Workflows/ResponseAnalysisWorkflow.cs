using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ConferenceAssistant.Agents.Definitions;
using ConferenceAssistant.Agents.Tools;

namespace ConferenceAssistant.Agents.Workflows;

public class ResponseAnalysisWorkflow(IChatClient chatClient, AgentTools tools)
{
    public async Task<string> ExecuteAsync(string pollId)
    {
        ChatClientAgent agent = new(
            chatClient,
            name: "ResponseAnalyst",
            description: "Analyzes poll results and generates insights",
            instructions: AgentDefinitions.ResponseAnalystInstructions,
            tools: tools.AsToolList());

        var workflow = AgentWorkflowBuilder.BuildSequential([agent]);

        var run = await InProcessExecution.Default.RunAsync(
            workflow,
            $"Analyze the results for poll ID: {pollId}. Use the tools to get the results, find context, identify trends, and generate actionable insights.");

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

        return "Unable to analyze poll results — no agent output collected.";
    }
}
