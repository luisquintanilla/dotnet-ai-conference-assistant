using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ConferenceAssistant.Agents.Tools;

namespace ConferenceAssistant.Agents.Workflows;

/// <summary>
/// Orchestrates a fan-out/fan-in workflow using AgentWorkflowBuilder.BuildConcurrent:
/// three specialized ChatClientAgents analyze polls, questions, and insights in parallel,
/// then results are merged and synthesized into a single session summary.
/// </summary>
public class SessionSummaryWorkflow(IChatClient chatClient, AgentTools tools)
{
    public async Task<string> ExecuteAsync()
    {
        var aiTools = tools.AsToolList();

        // Three specialized analysis agents run concurrently (fan-out)
        ChatClientAgent pollAnalyst = new(
            chatClient,
            name: "PollAnalyst",
            description: "Analyzes poll results and trends",
            instructions: """
                You are a poll analyst. Use GetAllPollResults to retrieve every poll
                and its results. Summarize the key findings: which options won,
                participation levels, and trends across polls. Be data-driven — cite percentages.
                """,
            tools: aiTools);

        ChatClientAgent questionAnalyst = new(
            chatClient,
            name: "QuestionAnalyst",
            description: "Analyzes audience questions and themes",
            instructions: """
                You are an audience question analyst. Use GetAudienceQuestions to retrieve
                all audience questions. Identify the top themes, most-upvoted questions,
                knowledge gaps (unanswered or high-interest questions), and overall curiosity patterns.
                """,
            tools: aiTools);

        ChatClientAgent insightAnalyst = new(
            chatClient,
            name: "InsightAnalyst",
            description: "Analyzes generated insights and knowledge patterns",
            instructions: """
                You are an insight analyst. Use GetAllInsights and SearchKnowledge to
                gather all generated insights and knowledge base content. Identify
                overarching themes, recurring patterns, and key takeaways from the session.
                """,
            tools: aiTools);

        // Build a concurrent workflow: all agents run in parallel, results merged
        var workflow = AgentWorkflowBuilder.BuildConcurrent(
            [pollAnalyst, questionAnalyst, insightAnalyst],
            MergeAgentOutputs);

        // Run the workflow via in-process execution
        var run = await InProcessExecution.Default.RunAsync(
            workflow,
            "Analyze the conference session data and provide your specialized findings.");

        // Collect all agent outputs from the run
        var agentOutputs = new List<string>();
        foreach (var evt in run.NewEvents)
        {
            if (evt is ExecutorCompletedEvent completed && completed.Data is IEnumerable<ChatMessage> msgs)
            {
                var text = string.Join("\n", msgs
                    .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
                    .Select(m => m.Text));
                if (!string.IsNullOrWhiteSpace(text))
                    agentOutputs.Add(text);
            }
        }

        // If we got outputs, synthesize into a unified summary
        if (agentOutputs.Count > 0)
        {
            return await SynthesizeSummaryAsync(agentOutputs);
        }

        return "Unable to generate session summary — no agent outputs collected.";
    }

    private async Task<string> SynthesizeSummaryAsync(List<string> agentOutputs)
    {
        var sections = new List<string>();
        string[] labels = ["Poll Analysis", "Question Analysis", "Insight Analysis"];
        for (int i = 0; i < agentOutputs.Count; i++)
        {
            var label = i < labels.Length ? labels[i] : $"Analysis {i + 1}";
            sections.Add($"## {label}\n{agentOutputs[i]}");
        }

        var synthesisPrompt = $"""
            You are a conference session summarizer. Below are analyses from specialized
            analysts. Synthesize them into one cohesive, narrative session summary (4-6 paragraphs).

            {string.Join("\n\n", sections)}
            """;

        var response = await chatClient.GetResponseAsync(synthesisPrompt);
        return response.Text ?? "Summary generation failed.";
    }

    private static List<ChatMessage> MergeAgentOutputs(IList<List<ChatMessage>> agentResults)
    {
        var merged = new List<ChatMessage>();
        foreach (var agentMessages in agentResults)
        {
            merged.AddRange(agentMessages);
        }
        return merged;
    }
}
