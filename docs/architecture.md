# Conference Pulse — Architecture

## System Architecture

```
┌────────────────────────────────────────────────────────────┐
│         Blazor Interactive Server (.NET 10)                 │
│                                                            │
│  /presenter    → Speaker dashboard + controls              │
│  /session/{id} → Attendee participation (mobile-first)     │
│  /display      → Projection view (big screen)              │
│                                                            │
│  Real-time: SignalR (built into Blazor Server circuits)    │
└──────────────────────┬─────────────────────────────────────┘
                       │
┌──────────────────────▼─────────────────────────────────────┐
│         ASP.NET Core Host                                   │
│                                                            │
│  ┌── Agent Framework ────────────────────────────────┐     │
│  │  ChatClientAgents + AgentWorkflowBuilder          │     │
│  │  PollGeneration, ResponseAnalysis, SessionSummary │     │
│  └───────────────────────────────────────────────────┘     │
│                                                            │
│  ┌── M.E.AI ─────────────────────────────────────────┐     │
│  │  IChatClient → ChatClientBuilder pipeline         │     │
│  │  IEmbeddingGenerator, AIFunctionFactory           │     │
│  └───────────────────────────────────────────────────┘     │
│                                                            │
│  ┌── DataIngestion ──────────────────────────────────┐     │
│  │  IngestionPipeline<ConferenceRecord>              │     │
│  │  MarkdigReader, Chunkers, Enrichers, Writer       │     │
│  └───────────────────────────────────────────────────┘     │
│                                                            │
│  ┌── VectorData ─────────────────────────────────────┐     │
│  │  InMemoryVectorStore (auto-embedding)             │     │
│  │  VectorStoreCollection + SearchAsync()            │     │
│  └───────────────────────────────────────────────────┘     │
│                                                            │
│  ┌── MCP ────────────────────────────────────────────┐     │
│  │  SERVER (/mcp): 6 tools + 2 resources             │     │
│  │  CLIENTS: Microsoft Learn, DeepWiki               │     │
│  └───────────────────────────────────────────────────┘     │
└────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────┐
│  Copilot SDK Demo (console app)                             │
│  CopilotClient → McpServers → "Summarize this session"     │
└────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
dotnet-ai-conference-assistant/
├── src/
│   ├── ConferenceAssistant.AppHost/              # .NET Aspire AppHost
│   │   ├── ConferenceAssistant.AppHost.csproj
│   │   └── Program.cs
│   │
│   ├── ConferenceAssistant.ServiceDefaults/      # Aspire service defaults
│   │   ├── ConferenceAssistant.ServiceDefaults.csproj
│   │   └── Extensions.cs
│   │
│   ├── ConferenceAssistant.Web/                  # ASP.NET Core + Blazor Server
│   │   ├── ConferenceAssistant.Web.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Components/
│   │   │   ├── App.razor
│   │   │   ├── Routes.razor
│   │   │   ├── _Imports.razor
│   │   │   ├── Layout/
│   │   │   │   ├── PresentationLayout.razor
│   │   │   │   ├── PresentationLayout.razor.css
│   │   │   │   ├── DashboardLayout.razor
│   │   │   │   └── DashboardLayout.razor.css
│   │   │   ├── Pages/
│   │   │   │   ├── Home.razor
│   │   │   │   ├── Presenter.razor
│   │   │   │   ├── Session.razor
│   │   │   │   └── Display.razor
│   │   │   └── Shared/
│   │   │       ├── TopicDisplay.razor
│   │   │       ├── LivePoll.razor
│   │   │       ├── PollResults.razor
│   │   │       ├── InsightPanel.razor
│   │   │       ├── QuestionFeed.razor
│   │   │       └── AgentActivityLog.razor
│   │   ├── Services/
│   │   │   └── SessionStateService.cs
│   │   └── wwwroot/
│   │       └── css/
│   │           └── app.css
│   │
│   ├── ConferenceAssistant.CopilotDemo/          # Copilot SDK closer
│   │   ├── ConferenceAssistant.CopilotDemo.csproj
│   │   └── Program.cs
│   │
│   ├── ConferenceAssistant.Agents/               # Agent Framework layer
│   │   ├── ConferenceAssistant.Agents.csproj
│   │   ├── SurveyArchitectAgent.cs
│   │   ├── ResponseAnalystAgent.cs
│   │   ├── KnowledgeCuratorAgent.cs
│   │   ├── Tools/
│   │   │   ├── PollTools.cs
│   │   │   ├── KnowledgeTools.cs
│   │   │   └── InsightTools.cs
│   │   ├── Workflows/
│   │   │   ├── PollGenerationWorkflow.cs
│   │   │   ├── ResponseAnalysisWorkflow.cs
│   │   │   └── SessionSummaryWorkflow.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── ConferenceAssistant.Ingestion/            # DataIngestion pipelines
│   │   ├── ConferenceAssistant.Ingestion.csproj
│   │   ├── Models/
│   │   │   └── ConferenceRecord.cs
│   │   ├── Pipelines/
│   │   │   ├── OutlineIngestionPipeline.cs
│   │   │   ├── ResponseIngestionPipeline.cs
│   │   │   └── McpContentIngestionPipeline.cs
│   │   ├── Readers/
│   │   │   └── TextContentReader.cs
│   │   ├── SemanticSearchService.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── ConferenceAssistant.Mcp/                  # MCP server + clients
│   │   ├── ConferenceAssistant.Mcp.csproj
│   │   ├── Server/
│   │   │   ├── ConferenceTools.cs
│   │   │   └── ConferenceResources.cs
│   │   ├── Clients/
│   │   │   └── McpClientFactory.cs
│   │   └── DependencyInjection.cs
│   │
│   └── ConferenceAssistant.Core/                 # Shared domain
│       ├── ConferenceAssistant.Core.csproj
│       ├── Models/
│       │   ├── Poll.cs
│       │   ├── PollResponse.cs
│       │   ├── SessionTopic.cs
│       │   ├── Insight.cs
│       │   └── AudienceQuestion.cs
│       └── Services/
│           ├── PollService.cs
│           ├── SessionService.cs
│           └── InMemoryStore.cs
│
├── data/
│   ├── session-outline.md
│   └── seed-topics.json
│
├── tests/
│   ├── ConferenceAssistant.Agents.Tests/
│   │   └── ConferenceAssistant.Agents.Tests.csproj
│   ├── ConferenceAssistant.Ingestion.Tests/
│   │   └── ConferenceAssistant.Ingestion.Tests.csproj
│   └── ConferenceAssistant.Mcp.Tests/
│       └── ConferenceAssistant.Mcp.Tests.csproj
│
├── docs/
│   ├── plan.md
│   ├── prd.md
│   ├── architecture.md
│   ├── implementation-spec.md
│   └── session-outline.md
│
├── Directory.Packages.props
├── Directory.Build.props
├── ConferenceAssistant.sln
├── README.md
└── .gitignore
```

---

## NuGet Package Matrix

Each project lists only its DIRECT package references. Transitive dependencies are inherited.

### ConferenceAssistant.Core
```xml
<!-- No external NuGet packages — pure domain models and service interfaces -->
```

### ConferenceAssistant.Ingestion
```xml
<PackageReference Include="Microsoft.Extensions.AI.Abstractions" />
<PackageReference Include="Microsoft.Extensions.DataIngestion" />
<PackageReference Include="Microsoft.Extensions.DataIngestion.Markdig" />
<PackageReference Include="Microsoft.Extensions.VectorData.Abstractions" />
```
Project reference: `ConferenceAssistant.Core`

### ConferenceAssistant.Agents
```xml
<PackageReference Include="Microsoft.Agents.AI" />
<PackageReference Include="Microsoft.Agents.AI.Workflows" />
<PackageReference Include="Microsoft.Extensions.AI.Abstractions" />
```
Project references: `ConferenceAssistant.Core`, `ConferenceAssistant.Ingestion`

### ConferenceAssistant.Mcp
```xml
<PackageReference Include="ModelContextProtocol" />
<PackageReference Include="ModelContextProtocol.AspNetCore" />
<PackageReference Include="Microsoft.Extensions.AI.Abstractions" />
```
Project references: `ConferenceAssistant.Core`, `ConferenceAssistant.Ingestion`, `ConferenceAssistant.Agents`

### ConferenceAssistant.Web
```xml
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" />
<PackageReference Include="Microsoft.Agents.AI.OpenAI" />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.InMemory" />
<PackageReference Include="Microsoft.Extensions.DataIngestion" />
```
Project references: `ConferenceAssistant.Core`, `ConferenceAssistant.Ingestion`, `ConferenceAssistant.Agents`, `ConferenceAssistant.Mcp`

### ConferenceAssistant.CopilotDemo
```xml
<PackageReference Include="GitHub.Copilot.SDK" />
```

### ConferenceAssistant.AppHost
```xml
<PackageReference Include="Aspire.Hosting.AppHost" />
```
Project reference: `ConferenceAssistant.Web` (as Aspire resource)

### ConferenceAssistant.ServiceDefaults
```xml
<PackageReference Include="Microsoft.Extensions.Http.Resilience" />
<PackageReference Include="Microsoft.Extensions.ServiceDiscovery" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
```

---

## Data Flow

### Flow 1: Poll Generation
```
Speaker clicks "Generate Poll"
  → Presenter.razor sends event via SessionStateService
    → PollGenerationWorkflow starts
      → SurveyArchitect agent:
          1. Gets current topic from SessionService
          2. Calls search_knowledge tool → VectorStore.SearchAsync()
          3. Gets context about what audience already knows
          4. Generates poll question + options via IChatClient
          5. Calls create_poll tool → PollService.CreatePoll()
      → Poll in Draft status
  → Speaker reviews poll in Presenter dashboard
  → Speaker clicks "Launch"
    → PollService.LaunchPoll() → status = Active
      → SignalR pushes to all circuits
        → Session.razor shows voting UI
        → Display.razor shows live chart
```

### Flow 2: Response Analysis
```
Speaker clicks "Close Poll & Analyze"
  → PollService.ClosePoll()
    → ResponseIngestionPipeline ingests all responses
      → Chunked, sentiment-enriched, stored in VectorStore
    → ResponseAnalysisWorkflow starts
      → ResponseAnalyst agent:
          1. Calls get_poll_results tool → tallied results
          2. Searches vector store for audience context
          3. Generates insight via IChatClient
          4. Calls store_insight tool → InMemoryStore
      → KnowledgeCurator agent (handoff):
          1. Searches vector store for related content
          2. Optionally calls Microsoft Learn MCP for docs
          3. Ingests MCP response into vector store
          4. Enriches the insight with doc links
    → Insight pushed via SignalR
      → Display.razor InsightPanel updates
      → Presenter.razor shows detailed view
```

### Flow 3: Audience Q&A
```
Attendee types question in Session.razor
  → SessionStateService.AddQuestion()
    → Question ingested into vector store (keyword enriched)
    → KnowledgeCurator agent triggered:
        1. Searches vector store for relevant context
        2. Calls Microsoft Learn MCP if needed
        3. Ingests any new docs
        4. Generates answer via IChatClient
    → Answer attached to question
      → SignalR pushes update
        → QuestionFeed updates on all views
```

### Flow 4: Session Summary (The Closer)
```
Copilot SDK console app runs:
  CopilotClient → McpServers["conference-pulse"]
    → "Write a comprehensive summary"
      → Copilot discovers MCP tools
        → Calls generate_session_summary
          → Our MCP server routes to SessionSummaryWorkflow
            → Group chat: SurveyArchitect + ResponseAnalyst + KnowledgeCurator
              → Each searches vector store for their perspective
              → Collaborative summary generated
            → Summary returned as MCP tool result
          → Copilot formats and streams to console
```

---

## SignalR / Real-Time Strategy

Blazor Interactive Server uses SignalR circuits by default. We leverage this:

1. **SessionStateService** — singleton service holding all state. When state changes, it raises events.
2. **Components subscribe** — each Blazor component subscribes to relevant events in `OnInitializedAsync`.
3. **InvokeAsync(StateHasChanged)** — components call this when notified of changes.
4. **No additional SignalR hubs needed** — Blazor Server circuits handle everything.

```csharp
// SessionStateService pattern:
public class SessionStateService
{
    public event Action<Poll>? OnPollCreated;
    public event Action<Poll>? OnPollLaunched;
    public event Action<string, string>? OnVoteReceived;  // pollId, option
    public event Action<Insight>? OnInsightGenerated;
    public event Action<AudienceQuestion>? OnQuestionAsked;
    public event Action<AudienceQuestion>? OnQuestionAnswered;
    public event Action<SessionTopic>? OnTopicChanged;
    public event Action<string>? OnAgentActivity;  // activity description
    public event Action<string>? OnSummaryChunk;   // streaming summary text
}
```

---

## Configuration (appsettings.json)

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://{resource}.openai.azure.com/",
    "ChatDeployment": "gpt-4o",
    "EmbeddingDeployment": "text-embedding-3-small",
    "ApiKey": ""
  },
  "Session": {
    "Code": "AICONF",
    "OutlinePath": "data/session-outline.md",
    "TopicsPath": "data/seed-topics.json"
  },
  "Mcp": {
    "ServerPath": "/mcp",
    "Clients": {
      "MicrosoftLearn": "https://learn.microsoft.com/api/mcp",
      "DeepWiki": "https://deepwiki.com/api/mcp"
    }
  }
}
```

Environment variable overrides:
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_API_KEY`
- `AZURE_OPENAI_CHAT_DEPLOYMENT`
- `AZURE_OPENAI_EMBEDDING_DEPLOYMENT`
