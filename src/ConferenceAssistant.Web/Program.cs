using Microsoft.Extensions.AI;
using ConferenceAssistant.Core.Services;
using ConferenceAssistant.Ingestion.Services;
using ConferenceAssistant.Agents.Tools;
using ConferenceAssistant.Agents.Workflows;
using ConferenceAssistant.Mcp.Clients;
using ConferenceAssistant.Web.Components;
using ConferenceAssistant.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Aspire Service Defaults (OpenTelemetry, health checks, service discovery)
// ---------------------------------------------------------------------------
builder.AddServiceDefaults();

// ---------------------------------------------------------------------------
// Blazor Interactive Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// Core Services — in-memory, singleton (shared state across all Blazor circuits)
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<IPollService, PollService>();
builder.Services.AddSingleton<IQuestionService, QuestionService>();
builder.Services.AddSingleton<IInsightService, InsightService>();

// ---------------------------------------------------------------------------
// Aspire AI Integration — Azure OpenAI via Aspire connection
// Connection string injected by AppHost via WithReference(openai)
// ---------------------------------------------------------------------------
var openaiBuilder = builder.AddAzureOpenAIClient("openai");

openaiBuilder.AddChatClient("chat")
    .UseFunctionInvocation()
    .UseOpenTelemetry()
    .UseLogging();

openaiBuilder.AddEmbeddingGenerator("embedding");

// ---------------------------------------------------------------------------
// Ingestion + VectorData — knowledge base pipeline
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddSingleton<IIngestionService, IngestionService>();

// ---------------------------------------------------------------------------
// Agent Tools + Workflows
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<AgentTools>();
builder.Services.AddSingleton<PollGenerationWorkflow>();
builder.Services.AddSingleton<ResponseAnalysisWorkflow>();
builder.Services.AddSingleton<SessionSummaryWorkflow>();

// ---------------------------------------------------------------------------
// MCP Server — expose tools via Streamable HTTP
// ---------------------------------------------------------------------------
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "ConferencePulse",
            Version = "1.0.0"
        };
    })
    .WithToolsFromAssembly(typeof(ConferenceAssistant.Mcp.Tools.ConferenceTools).Assembly)
    .WithHttpTransport();

// ---------------------------------------------------------------------------
// AI-powered Q&A — auto-answer audience questions
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IQuestionAnsweringService, QuestionAnsweringService>();

// ---------------------------------------------------------------------------
// AI-powered Insight Generation — auto-generate on topic complete + poll close
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IInsightGenerationService, InsightGenerationService>();

// ---------------------------------------------------------------------------
// MCP Client — consume external MCP servers
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IMcpContentClient, McpContentClient>();

// ---------------------------------------------------------------------------
// Build app
// ---------------------------------------------------------------------------
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// MCP Streamable HTTP endpoint at /mcp
app.MapMcp("/mcp");

// Aspire health check endpoints
app.MapDefaultEndpoints();

// ---------------------------------------------------------------------------
// Wire up AI-powered Q&A: auto-answer when audience submits a question
// ---------------------------------------------------------------------------
var questionService = app.Services.GetRequiredService<IQuestionService>();
var questionAnswering = app.Services.GetRequiredService<IQuestionAnsweringService>();
questionService.QuestionReceived += q =>
{
    _ = Task.Run(() => questionAnswering.GenerateAiAnswerAsync(q.Id, q.Text, q.TopicId));
};

// ---------------------------------------------------------------------------
// Wire up real-time ingestion: feed new data into the vector store as it arrives
// ---------------------------------------------------------------------------
var pollService = app.Services.GetRequiredService<IPollService>();
var insightService = app.Services.GetRequiredService<IInsightService>();
var ingestionService = app.Services.GetRequiredService<IIngestionService>();
var insightGen = app.Services.GetRequiredService<IInsightGenerationService>();

// Ingest poll results when a poll is closed + generate poll insights
pollService.PollClosed += poll =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            var results = pollService.GetPollResults(poll.Id);
            if (results.Count > 0)
            {
                await ingestionService.IngestResponseAsync(poll.Id, poll.TopicId, poll.Question, results);
                app.Logger.LogInformation("Ingested poll results for {PollId} into knowledge base", poll.Id);
            }
            await insightGen.GeneratePollInsightsAsync(poll.Id);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Failed to ingest poll results for {PollId}", poll.Id);
        }
    });
};

// Ingest Q&A pairs when a question is answered
questionService.QuestionAnswered += q =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            var latestAnswer = q.Answers.LastOrDefault();
            if (latestAnswer is null) return;
            var badge = latestAnswer.IsAiGenerated ? "[AI]" : "[Human]";
            var content = $"Q: {q.Text}\nA {badge}: {latestAnswer.Text}";
            await ingestionService.IngestExternalContentAsync("qa", content);
            app.Logger.LogInformation("Ingested Q&A pair into knowledge base ({Badge})", badge);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Failed to ingest Q&A pair");
        }
    });
};

// Ingest insights into the knowledge base when generated
insightService.InsightGenerated += insight =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            await ingestionService.IngestInsightAsync(insight.TopicId ?? "general", insight.Content);
            app.Logger.LogInformation("Ingested insight into knowledge base");
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Failed to ingest insight");
        }
    });
};

// Generate topic summary + gap insights when a topic is completed
var sessionSvc = app.Services.GetRequiredService<ISessionService>();
sessionSvc.TopicCompleted += topicId =>
{
    _ = Task.Run(() => insightGen.GenerateTopicInsightsAsync(topicId));
};

// ---------------------------------------------------------------------------
// Startup: Load session from seed data + ingest outline into vector store
// ---------------------------------------------------------------------------
var dataRoot = Path.Combine(app.Environment.ContentRootPath, "..", "..", "data");

var sessionService = app.Services.GetRequiredService<ISessionService>();
await sessionService.LoadSessionAsync(Path.Combine(dataRoot, "seed-topics.json"));
app.Logger.LogInformation("Session loaded: {Title}", sessionService.CurrentSession?.Title);

// Ingest outline in the background (requires AI provider)
_ = Task.Run(async () =>
{
    try
    {
        var ingestion = app.Services.GetRequiredService<IIngestionService>();
        var outlinePath = Path.Combine(dataRoot, "session-outline.md");
        if (File.Exists(outlinePath))
        {
            var count = await ingestion.IngestOutlineAsync(outlinePath);
            app.Logger.LogInformation("Ingested {Count} outline chunks into knowledge base", count);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Outline ingestion skipped (AI provider may not be configured)");
    }
});

app.Run();
