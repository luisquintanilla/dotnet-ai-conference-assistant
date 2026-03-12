using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using ConferenceAssistant.Core.Services;
using ConferenceAssistant.Ingestion.Services;
using ConferenceAssistant.Agents.Tools;
using ConferenceAssistant.Agents.Workflows;
using ConferenceAssistant.Mcp.Clients;
using ConferenceAssistant.Web.Components;

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
// Microsoft.Extensions.AI — IChatClient + IEmbeddingGenerator
// Configured via AI:Endpoint, AI:ApiKey, AI:ChatModel, AI:EmbeddingModel
// ---------------------------------------------------------------------------
var aiEndpoint = builder.Configuration["AI:Endpoint"];
var aiKey = builder.Configuration["AI:ApiKey"];
var chatModel = builder.Configuration["AI:ChatModel"] ?? "gpt-4o";
var embeddingModel = builder.Configuration["AI:EmbeddingModel"] ?? "text-embedding-3-small";

if (!string.IsNullOrEmpty(aiEndpoint) && !string.IsNullOrEmpty(aiKey))
{
    // Azure OpenAI
    var azureClient = new AzureOpenAIClient(
        new Uri(aiEndpoint),
        new ApiKeyCredential(aiKey));

    builder.Services.AddChatClient(azureClient.GetChatClient(chatModel).AsIChatClient())
        .UseFunctionInvocation()
        .UseOpenTelemetry()
        .UseLogging();

    builder.Services.AddEmbeddingGenerator(
        azureClient.GetEmbeddingClient(embeddingModel).AsIEmbeddingGenerator());
}
else if (!string.IsNullOrEmpty(aiKey))
{
    // OpenAI (direct)
    var openAiClient = new OpenAI.OpenAIClient(new ApiKeyCredential(aiKey));

    builder.Services.AddChatClient(openAiClient.GetChatClient(chatModel).AsIChatClient())
        .UseFunctionInvocation()
        .UseOpenTelemetry()
        .UseLogging();

    builder.Services.AddEmbeddingGenerator(
        openAiClient.GetEmbeddingClient(embeddingModel).AsIEmbeddingGenerator());
}
else
{
    builder.Logging.AddConsole();
    Console.WriteLine("⚠️  No AI provider configured. Set AI:Endpoint + AI:ApiKey to enable AI features.");
}

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
    .WithToolsFromAssembly(typeof(ConferenceAssistant.Mcp.Tools.ConferenceTools).Assembly);

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
app.MapMcp();

// Aspire health check endpoints
app.MapDefaultEndpoints();

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
