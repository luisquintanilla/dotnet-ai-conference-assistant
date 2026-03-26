# Conference Pulse — The Living Presentation

> Not a presentation *about* AI — a presentation *that is* AI.

An interactive .NET conference assistant that runs as a live web app. No slides. The presentation surface itself is powered by the Microsoft AI stack — agents generate polls, analyze audience responses, build a growing knowledge base, and produce real-time insights. Each segment makes the app smarter (the "snowball effect").

## The Five Technologies

| # | Technology | Role |
|---|-----------|------|
| 1 | **Microsoft.Extensions.AI** | The abstraction layer — `IChatClient`, `IEmbeddingGenerator`, `ChatClientBuilder` middleware |
| 2 | **Microsoft.Extensions.DataIngestion** | Feeding the brain — markdown → chunks → enrichment → vector store |
| 3 | **Microsoft.Extensions.VectorData** | The memory — `InMemoryVectorStore` with semantic search |
| 4 | **Microsoft Agent Framework** | The intelligence — specialized agents with tools and workflows |
| 5 | **Model Context Protocol (MCP)** | The bridge — our app as both MCP server and client |

**The Closer:** Speaker opens Copilot CLI → points it at our MCP server → types "Summarize this session" → cascade visualization lights up on the big screen as all five technologies fire in sequence.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Preview)
- [.NET Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)
- An Azure OpenAI resource with `chat` (e.g. gpt-4o) and `embedding` (e.g. text-embedding-3-small) deployments
- Azure CLI logged in (`az login`) — authentication uses `DefaultAzureCredential`

## Quick Start

```bash
# Clone
git clone https://github.com/your-org/dotnet-ai-conference-assistant.git
cd dotnet-ai-conference-assistant

# Set user secrets (one-time)
cd src/ConferenceAssistant.AppHost

# Azure local provisioning (required by Aspire for Azure resources)
dotnet user-secrets set "Azure:SubscriptionId" "your-azure-subscription-id"
dotnet user-secrets set "Azure:Location" "eastus"

# Azure OpenAI resource reference
dotnet user-secrets set "AzureOpenAI:Name" "your-openai-resource-name"
dotnet user-secrets set "AzureOpenAI:ResourceGroup" "your-resource-group"
cd ../..

# Ensure you're logged in to Azure
az login

# Run with Aspire
aspire run
```

Open the three views (port shown in Aspire dashboard):
- **`/presenter`** — Speaker dashboard (laptop)
- **`/display`** — Projection view (big screen)
- **`/session/DOTNETAI-CONF`** — Attendee participation (audience phones via QR code)

## Architecture

```
┌─────────────────────────────────────────────────┐
│  Blazor Interactive Server (.NET 10)            │
│  /presenter  /display  /session/{id}            │
└──────────────────┬──────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────┐
│  ASP.NET Core Host                              │
│                                                 │
│  ┌─ Agent Framework ─────────────────────────┐  │
│  │  SurveyArchitect · ResponseAnalyst        │  │
│  │  KnowledgeCurator · Workflows             │  │
│  └───────────────────────────────────────────┘  │
│  ┌─ M.E.AI ──────────────────────────────────┐  │
│  │  IChatClient → ChatClientBuilder pipeline │  │
│  │  IEmbeddingGenerator · AIFunctionFactory  │  │
│  └───────────────────────────────────────────┘  │
│  ┌─ DataIngestion ───────────────────────────┐  │
│  │  Outline · Responses · Insights · MCP     │  │
│  └───────────────────────────────────────────┘  │
│  ┌─ VectorData ──────────────────────────────┐  │
│  │  InMemoryVectorStore + SemanticSearch      │  │
│  └───────────────────────────────────────────┘  │
│  ┌─ MCP ─────────────────────────────────────┐  │
│  │  SERVER: 8 tools at /mcp                  │  │
│  │  CLIENT: External knowledge sources       │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

## Project Structure

```
src/
├── ConferenceAssistant.Web/          # Blazor Server + Program.cs DI wiring
├── ConferenceAssistant.Core/         # Domain models + in-memory services
├── ConferenceAssistant.Agents/       # Agent tools, definitions, workflows
├── ConferenceAssistant.Ingestion/    # DataIngestion pipelines + vector search
├── ConferenceAssistant.Mcp/          # MCP server tools + external clients
├── ConferenceAssistant.AppHost/      # .NET Aspire orchestration
└── ConferenceAssistant.ServiceDefaults/  # Aspire service defaults
data/
├── seed-topics.json                  # Pre-configured session topics + polls
├── session-outline.md                # Session content (ingested at startup)
└── slides.md                         # Markdown slide deck (display + knowledge base)
docs/
├── plan.md                           # Master plan
├── architecture.md                   # Technical architecture
├── implementation-spec.md            # Implementation specification
├── session-outline.md                # Presentation flow
└── smoke-test.md                     # Setup & testing guide
```

## MCP Server

The app exposes an MCP server at `/mcp` with these tools:

| Tool | Description |
|------|-------------|
| `get_session_status` | Session title, status, topics |
| `get_active_poll` | Current poll with vote counts |
| `get_poll_results` | Detailed results for a poll |
| `search_session_knowledge` | Semantic search over knowledge base |
| `get_audience_questions` | Top audience questions |
| `get_topic_insights` | Insights for a topic |
| `get_all_insights` | All generated insights |
| `generate_session_summary` | Full agent workflow → summary |

### Copilot CLI Integration

```jsonc
// .vscode/mcp.json (included in the repo's .gitignore)
{
  "servers": {
    "ConferencePulse": {
      "type": "http",
      "url": "https://localhost:7231/mcp"
    }
  }
}
```

> ⚠️ Port may vary — check the Aspire dashboard for the actual web endpoint.

Then: `"Summarize this session including all poll results and key themes"`

## Configuration

All AI configuration flows through **Aspire + user secrets** — no API keys in code or config files.

| User Secret | Description |
|-------------|-------------|
| `Azure:SubscriptionId` | Your Azure subscription ID (Aspire local provisioning) |
| `Azure:Location` | Azure region for provisioned resources (e.g. `eastus`) |
| `AzureOpenAI:Name` | Your Azure OpenAI resource name |
| `AzureOpenAI:ResourceGroup` | Resource group containing the resource |

Authentication uses `DefaultAzureCredential` (Azure CLI, managed identity, etc.). Azure OpenAI deployment names (`chat`, `embedding`) are configured in the AppHost.

## The Snowball Effect

Each segment enriches the vector store. Agents in later segments have richer context:

```
Segment 1: outline only        → basic poll
Segment 2: + poll responses     → contextual poll
Segment 3: + insights           → trend-aware poll
Segment 4: + MCP docs           → maximum context
Closer:    FULL knowledge base  → comprehensive summary
```

## Slide System

The presentation content is authored in Markdown (`data/slides.md`) using simple conventions:
- `---` separates slides
- `<!-- speaker: notes -->` adds presenter-only speaker notes
- `<!-- topic: id -->` maps slides to session topics

The big screen (`/display`) shows slides full-screen between polls. The presenter (`/presenter`) sees the current slide, speaker notes, and a preview of the next slide — like PowerPoint Presenter View.

See [docs/slide-authoring-guide.md](docs/slide-authoring-guide.md) for the full authoring reference.

## License

[MIT](LICENSE)
