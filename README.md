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
- Azure OpenAI or OpenAI API key
- [.NET Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling) (for the dashboard)

## Quick Start

```bash
# Clone and configure
git clone https://github.com/your-org/dotnet-ai-conference-assistant.git
cd dotnet-ai-conference-assistant

# Set your AI provider credentials
# Option A: Azure OpenAI
export AI__Endpoint="https://your-resource.openai.azure.com/"
export AI__ApiKey="your-api-key"
export AI__ChatModel="gpt-4o"
export AI__EmbeddingModel="text-embedding-3-small"

# Option B: OpenAI (omit Endpoint)
export AI__ApiKey="sk-..."

# Run with Aspire
dotnet run --project src/ConferenceAssistant.AppHost

# Or run the web app directly
dotnet run --project src/ConferenceAssistant.Web
```

Open the three views:
- **`/presenter`** — Speaker dashboard (laptop)
- **`/display`** — Projection view (big screen)
- **`/session/AICONF`** — Attendee participation (audience phones via QR code)

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
└── session-outline.md                # Session content (ingested at startup)
docs/
├── plan.md                           # Master plan
├── architecture.md                   # Technical architecture
├── implementation-spec.md            # Implementation specification
└── session-outline.md                # Presentation flow
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
// Add to your MCP config (e.g., ~/.config/github-copilot/mcp.json)
{
  "servers": {
    "conference-pulse": {
      "url": "https://localhost:5001/mcp"
    }
  }
}
```

Then: `"Summarize this session including all poll results and key themes"`

## Configuration

All settings via environment variables or `appsettings.json`:

| Setting | Default | Description |
|---------|---------|-------------|
| `AI:Endpoint` | — | Azure OpenAI endpoint (omit for direct OpenAI) |
| `AI:ApiKey` | — | API key |
| `AI:ChatModel` | `gpt-4o` | Chat completion model |
| `AI:EmbeddingModel` | `text-embedding-3-small` | Embedding model |

## The Snowball Effect

Each segment enriches the vector store. Agents in later segments have richer context:

```
Segment 1: outline only        → basic poll
Segment 2: + poll responses     → contextual poll
Segment 3: + insights           → trend-aware poll
Segment 4: + MCP docs           → maximum context
Closer:    FULL knowledge base  → comprehensive summary
```

## License

[MIT](LICENSE)
