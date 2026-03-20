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
builder.Services.AddSingleton<ISessionManager, SessionManager>();
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
builder.Services.AddSingleton<ISessionDraftingService, SessionDraftingService>();
builder.Services.AddSingleton<IContentImportService, ContentImportService>();

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
// Wire up AI-powered pipelines via SessionCreated event
// When any session is created (startup demo or user-created), wire AI handlers
// ---------------------------------------------------------------------------
var sessionManager = app.Services.GetRequiredService<ISessionManager>();
var questionAnswering = app.Services.GetRequiredService<IQuestionAnsweringService>();
var ingestionService = app.Services.GetRequiredService<IIngestionService>();
var insightGen = app.Services.GetRequiredService<IInsightGenerationService>();

sessionManager.SessionCreated += ctx =>
{
    app.Logger.LogInformation("Session created: {Code} — {Title}", ctx.Session.SessionCode, ctx.Session.Title);

    // Auto-answer audience questions via AI
    ctx.QuestionReceived += q =>
    {
        _ = Task.Run(() => questionAnswering.GenerateAiAnswerAsync(q.Id, q.Text, q.TopicId));
    };

    // Ingest poll results when a poll is closed + generate poll insights
    ctx.PollClosed += poll =>
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var results = ctx.GetPollResults(poll.Id);
                if (results.Count > 0)
                {
                    await ingestionService.IngestResponseAsync(poll.Id, poll.TopicId, poll.Question, results);
                    app.Logger.LogInformation("Ingested poll results for {PollId} into knowledge base", poll.Id);
                }
                await insightGen.GeneratePollInsightsAsync(poll.Id);
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Failed to process poll close for {PollId}", poll.Id);
            }
        });
    };

    // Ingest Q&A pairs when a question is answered
    ctx.QuestionAnswered += q =>
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
    ctx.InsightGenerated += insight =>
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
    ctx.TopicCompleted += topicId =>
    {
        _ = Task.Run(() => insightGen.GenerateTopicInsightsAsync(topicId));
    };

    // Ingest questions immediately when received (before they're answered)
    ctx.QuestionReceived += q =>
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ingestionService.IngestQuestionAsync(q.Id, q.Text, q.TopicId);
                app.Logger.LogInformation("Ingested question {QuestionId} into knowledge base", q.Id);
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Failed to ingest question {QuestionId}", q.Id);
            }
        });
    };

    // Generate session summary on session end
    ctx.SessionEnded += () =>
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var workflow = app.Services.GetRequiredService<SessionSummaryWorkflow>();
                var summary = await workflow.ExecuteAsync();
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    await ingestionService.IngestSessionSummaryAsync(summary);
                    app.Logger.LogInformation("Session summary generated and ingested into knowledge base");
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Failed to generate/ingest session summary");
            }
        });
    };
};

// ---------------------------------------------------------------------------
// Startup: Load default demo session, ingest outline, initialize MCP clients
// ---------------------------------------------------------------------------
var dataRoot = Path.Combine(app.Environment.ContentRootPath, "..", "..", "data");

// Create the default demo session (this fires SessionCreated, which wires all AI pipelines)
var sessionService = app.Services.GetRequiredService<ISessionService>();
await sessionService.LoadSessionAsync(Path.Combine(dataRoot, "seed-topics.json"));
app.Logger.LogInformation("Default session loaded: {Title} (code: {Code}, PIN: {Pin})",
    sessionService.CurrentSession?.Title,
    sessionService.CurrentSession?.SessionCode,
    "0000");

// Load slides from Markdown
var slidesPath = Path.Combine(dataRoot, "slides.md");
if (File.Exists(slidesPath))
{
    await sessionService.LoadSlidesAsync(slidesPath);
    app.Logger.LogInformation("Slides loaded: {Count} slides", sessionService.TotalSlides);
}
else
{
    app.Logger.LogWarning("No slides.md found at {Path} — slide features disabled", slidesPath);
}

// Initialize MCP client connections (Microsoft Learn + DeepWiki) in the background
_ = Task.Run(async () =>
{
    try
    {
        var mcpClient = app.Services.GetRequiredService<IMcpContentClient>();
        await mcpClient.InitializeAsync();
        app.Logger.LogInformation("MCP clients initialized (Microsoft Learn + DeepWiki)");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "MCP client initialization failed — doc-augmented answers unavailable");
    }
});

// Ingest outline at startup — await to ensure knowledge base is seeded before serving requests
try
{
    var outlinePath = Path.Combine(dataRoot, "session-outline.md");
    if (File.Exists(outlinePath))
    {
        var count = await ingestionService.IngestOutlineAsync(outlinePath);
        app.Logger.LogInformation("Ingested {Count} outline chunks into knowledge base", count);
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Outline ingestion skipped (AI provider may not be configured)");
}

app.Run();
