# 🧪 Conference Pulse — Smoke Test & Getting Started Guide

> **Updated for Aspire AI integration + real-time ingestion + AI answers + insight generation**
> All AI configuration flows through Aspire's `AddAzureOpenAI` + `RunAsExisting` + user secrets.

---

## 0. Prerequisites

### Tools
```powershell
# .NET 10 SDK
dotnet --list-sdks          # must show 10.x.x

# Aspire workload (install if missing)
dotnet workload list        # should show "aspire"
dotnet workload install aspire   # if not listed
```

### Azure OpenAI Resource

You need an **existing** Azure OpenAI resource with **two deployments**:

| Deployment Name | Model                    | Purpose           |
|-----------------|--------------------------|--------------------|
| `chat`          | gpt-4o (2024-08-06)     | Chat / agents      |
| `embedding`     | text-embedding-3-small   | Vector embeddings  |

> ⚠️ The deployment names in Azure must match exactly: **`chat`** and **`embedding`**.
> If your deployments are named differently (e.g., `gpt-4o`, `my-chat`), update
> `AppHost.cs` lines 15-16 and `Program.cs` lines 36, 41 to match.

### User Secrets (one-time setup)

```powershell
cd C:\Dev\dotnet-ai-conference-assistant\src\ConferenceAssistant.AppHost

# Your Azure OpenAI resource name (not the full URL — just the name)
dotnet user-secrets set "AzureOpenAI:Name" "my-openai-resource"

# The resource group it lives in
dotnet user-secrets set "AzureOpenAI:ResourceGroup" "my-resource-group"
```

Aspire reads these via `AddParameterFromConfiguration` → passes them to
`RunAsExisting` → resolves the endpoint → injects the connection string into
the web project automatically.

**No API keys in config!** Aspire uses `DefaultAzureCredential` (your Azure
CLI login, managed identity, etc.). Make sure you're logged in:
```powershell
az login
az account set --subscription "<your-subscription-id>"
```

> If you prefer API key auth, you can set the connection string directly in
> AppHost user secrets instead:
> ```powershell
> dotnet user-secrets set "ConnectionStrings:openai" "Endpoint=https://my-resource.openai.azure.com/;Key=YOUR-KEY"
> ```
> This bypasses the `RunAsExisting` flow entirely.

---

## 1. Build & Launch

```powershell
cd C:\Dev\dotnet-ai-conference-assistant

# Build everything (should be 0 errors, 0 warnings)
dotnet build

# Launch via Aspire CLI (recommended) or project
aspire run
# OR: dotnet run --project src/ConferenceAssistant.AppHost
```

### ✅ What you should see

```
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 13.1.2
      Dashboard is running at: http://localhost:18888/login?t=<token>
      ...
info: ConferenceAssistant.Web
      Session loaded: The Microsoft AI Stack for .NET
      Ingested 12 outline chunks into knowledge base
```

### Key URLs

| URL | What |
|-----|------|
| **Aspire Dashboard** | `http://localhost:18888` (shown in console output) |
| **Web App** | Check the dashboard → `web` resource → click the endpoint (typically `https://localhost:<port>`) |

> The web app port is **dynamically assigned by Aspire** — don't guess it.
> Always get it from the Aspire dashboard's Resources tab.

### If AI isn't configured

The app still starts! You'll see:
```
warn: Outline ingestion skipped (AI provider may not be configured)
```
All non-AI features (manual polls, voting, questions, topic management) work fine.

---

## 2. Aspire Dashboard — `http://localhost:18888`

| # | Check | Expected |
|---|-------|----------|
| 1 | Dashboard loads | Login page → click through with token from console |
| 2 | Resources tab | Shows **`web`** (ASP.NET Core) and **`openai`** (Azure OpenAI) |
| 3 | `web` resource status | Running / Healthy |
| 4 | `openai` resource status | Healthy (if secrets configured) |
| 5 | Click `web` endpoint link | Opens the app in browser |
| 6 | Structured Logs tab | Shows startup messages |
| 7 | Traces tab | Shows HTTP request traces with OpenTelemetry spans |

---

## 3. Home Page — `/`

| # | Check | Expected |
|---|-------|----------|
| 1 | Page loads | Hero: "🎯 Conference Pulse" + "AI-Powered Conference Assistant" |
| 2 | Three cards visible | Presenter Dashboard, Join Session, Projection Display |
| 3 | Click "Presenter Dashboard" | Navigates to `/presenter` |
| 4 | Click "Join Session" | Navigates to `/session/DOTNETAI-CONF` |
| 5 | Click "Projection Display" | Navigates to `/display` |

---

## 4. Presenter Dashboard — `/presenter`

Open this in your "speaker laptop" browser window.

### 4a. Session Setup State

| # | Check | Expected |
|---|-------|----------|
| 1 | Session title shows | "The Microsoft AI Stack for .NET" |
| 2 | Status badge | Shows "Setup" |
| 3 | Knowledge base counter | Shows "📚 X records" (X > 0 if AI configured) |
| 4 | 5 topics in left panel | meai, knowledge, agents, mcp, closer |
| 5 | "🚀 Go Live" button visible | Yes |
| 6 | Topic activate buttons | Should NOT appear (session not live yet) |

### 4b. Go Live

| # | Action | Expected |
|---|--------|----------|
| 1 | Click **🚀 Go Live** | Status badge changes to "Live" |
| 2 | Button changes | Now shows "⏹ End Session" |
| 3 | Topics | Each shows **▶ Activate** button |

### 4c. Topic Lifecycle

| # | Action | Expected |
|---|--------|----------|
| 1 | Click **▶ Activate** on "Microsoft.Extensions.AI" | Topic becomes active, main panel shows title + description + talking points |
| 2 | Active topic badge | Shows "Active" status |
| 3 | Other topics | Still show "Upcoming" |
| 4 | Click **✓ Complete** on active topic | Status changes to "Completed" |
| 5 | Activate next topic | New topic becomes active, previous stays completed |

### 4d. Polls — Suggested

| # | Action | Expected |
|---|--------|----------|
| 1 | Activate topic "meai" | Poll section appears |
| 2 | Dropdown shows suggested polls | "What's your experience level with AI in .NET?" etc. |
| 3 | Select a poll from dropdown | **Launch** button becomes enabled |
| 4 | Click **Launch** | Poll appears with options + vote counts (all 0) |
| 5 | Poll status | Shows as "Draft" with **📡 Go Live** button |
| 6 | Click **📡 Go Live** | Poll becomes Active |
| 7 | Click **🔒 Close Poll** | Poll closes, results frozen |

### 4e. Polls — Auto-Generate (🤖 requires AI)

| # | Action | Expected |
|---|--------|----------|
| 1 | Click **🤖 Auto-Generate Poll** | AI agent generates a contextual poll |
| 2 | Poll appears | Question + options rendered |

### 4f. Polls — Custom

| # | Action | Expected |
|---|--------|----------|
| 1 | Click **✏️ Custom Poll** | Form with question input + 2 option fields |
| 2 | Fill in question + ≥2 options | **🚀 Create & Go Live** becomes enabled |
| 3 | Click **🚀 Create & Go Live** | Custom poll created and goes live |

---

## 5. Attendee Session — `/session/DOTNETAI-CONF`

Open in a second browser window (or phone).

### 5a. Before Go Live

| # | Check | Expected |
|---|-------|----------|
| 1 | Navigate to page | "⏳ Session hasn't started yet. Hang tight!" |

### 5b. Voting (after Go Live + active poll)

| # | Action | Expected |
|---|--------|----------|
| 1 | Click a poll option | Vote registered |
| 2 | Results update | Vote count increments |
| 3 | Switch to Presenter tab | Presenter sees updated counts + percentages |

### 5c. Questions + AI Auto-Answer

| # | Action | Expected |
|---|--------|----------|
| 1 | Type in "Ask a Question" box | Text appears |
| 2 | Click **Send** | Question submitted, input clears |
| 3 | Question in "🔥 Top Questions" | With 👍 0 count |
| 4 | Click 👍 on a question | Count increments |
| 5 | Switch to Presenter tab | Question visible in "❓ Audience Questions" |
| 6 | Wait 5-10 seconds | 🤖 AI answer appears automatically (blue-tinted, with AI badge) |
| 7 | AI answer uses KB context | Answer references session outline content |

### 5d. Answering / Overriding (from Presenter)

| # | Action | Expected |
|---|--------|----------|
| 1 | Question has no AI answer yet | **💬 Answer** button shows → type answer + submit |
| 2 | Question already has AI answer | **✏️ Override** button shows below the AI answer |
| 3 | Click **✏️ Override** | Text input appears to replace AI answer |
| 4 | Type human answer + Submit | AI answer replaced with human answer (💬 badge, no AI badge) |

---

## 6. Projection Display — `/display`

Open in a third browser window (simulates projector/big screen).

| # | Check | Expected |
|---|-------|----------|
| 1 | Before Go Live | "Waiting for session to begin..." |
| 2 | After Go Live | Session title + active topic in header |
| 3 | When poll active | PollResultsChart renders with live bar chart |
| 4 | Vote from Session tab | Display updates with new vote counts |
| 5 | When no active poll | Shows InsightCard or CascadeVisualization |

---

## 7. MCP Server — `/mcp`

The app exposes 10 MCP tools via Streamable HTTP. Get the base URL from
the Aspire dashboard (the `web` resource endpoint).

### 7a. Initialize + List Tools

```powershell
# Replace with your actual web app URL from Aspire dashboard
$baseUrl = "https://localhost:7174"

# Initialize MCP session
$init = @{
    jsonrpc = "2.0"; id = 1; method = "initialize"
    params = @{
        protocolVersion = "2025-03-26"
        capabilities = @{}
        clientInfo = @{ name = "smoke-test"; version = "1.0" }
    }
} | ConvertTo-Json -Depth 5

$resp = Invoke-RestMethod -Uri "$baseUrl/mcp" `
    -Method POST -ContentType "application/json" -Body $init
$resp | ConvertTo-Json -Depth 5

# List all tools
$listTools = @{ jsonrpc = "2.0"; id = 2; method = "tools/list" } | ConvertTo-Json

$tools = Invoke-RestMethod -Uri "$baseUrl/mcp" `
    -Method POST -ContentType "application/json" -Body $listTools
$tools | ConvertTo-Json -Depth 10
```

**✅ Expected:** 10 tools listed:

| # | Tool | Params | Requires AI |
|---|------|--------|-------------|
| 1 | `get_session_status` | none | No |
| 2 | `get_active_poll` | none | No |
| 3 | `get_poll_results` | `pollId` | No |
| 4 | `search_session_knowledge` | `query`, `maxResults?` | Yes (embeddings) |
| 5 | `get_audience_questions` | `count?` | No |
| 6 | `get_topic_insights` | `topicId` | No |
| 7 | `get_all_insights` | none | No |
| 8 | `generate_session_summary` | none | Yes (chat) |
| 9 | `search_knowledge` | `query` | Yes (embeddings) |
| 10 | `get_knowledge_stats` | none | No |

### 7b. Call a Tool

```powershell
$call = @{
    jsonrpc = "2.0"; id = 3; method = "tools/call"
    params = @{
        name = "get_session_status"
        arguments = @{}
    }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Uri "$baseUrl/mcp" `
    -Method POST -ContentType "application/json" -Body $call
```

**✅ Expected:** Returns session title, status, all 5 topics with statuses.

### 7c. Knowledge Search (requires AI)

```powershell
$search = @{
    jsonrpc = "2.0"; id = 4; method = "tools/call"
    params = @{
        name = "search_session_knowledge"
        arguments = @{ query = "embeddings"; maxResults = 3 }
    }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Uri "$baseUrl/mcp" `
    -Method POST -ContentType "application/json" -Body $search
```

**✅ Expected:** Returns matching knowledge base records from ingested outline.

---

## 8. Real-Time Ingestion — Knowledge Base Growth

The knowledge base starts with ~20 outline chunks and **grows in real-time** as the
session progresses. This is the "snowball effect" — the core demo narrative.

### What Gets Ingested

| Event | What's Ingested | Trigger |
|-------|----------------|---------|
| App startup | Session outline chunks (~20) | `IngestOutlineAsync` at boot |
| Poll closed | Poll question + all option results + vote counts | `PollClosed` event |
| Question answered | Q&A pair (question text + answer text) | `QuestionAnswered` event |
| Insight generated | Insight content (poll analysis, topic summary, etc.) | `InsightGenerated` event |

### How to Verify

| # | Action | Expected |
|---|--------|----------|
| 1 | Start app, check KB count on Presenter | Shows ~20 records (outline chunks) |
| 2 | Launch a poll → vote → close poll | KB count increases (poll results ingested) |
| 3 | Submit a question → wait for AI answer | KB count increases (Q&A pair ingested) |
| 4 | Complete a topic | KB count increases (topic insights ingested) |
| 5 | Use MCP `search_session_knowledge` query | Returns results from polls, Q&A, and insights — not just outline |

> 💡 The Presenter dashboard KB counter refreshes automatically when insights are generated.
> You can also call `get_knowledge_stats` via MCP to see the exact count.

---

## 9. Insight Generation — Auto-Analysis

Insights are **automatically generated** by AI when:

1. **A poll is closed** → `PollAnalysis` insight with vote breakdown and takeaways
2. **A topic is completed** → `TopicSummary` insight + `KnowledgeGap` insight (from unanswered questions)

### How to Test

| # | Action | Expected |
|---|--------|----------|
| 1 | Activate topic → launch poll → vote → close poll | Wait 5-10s → poll analysis insight appears in Insights panel |
| 2 | Complete the topic | Wait 5-10s → topic summary + knowledge gap insights appear |
| 3 | Check Presenter "💡 Insights" section | Shows insight cards with type badges (📊 Poll Analysis, 📋 Topic Summary, 🔍 Knowledge Gap) |
| 4 | Insights also ingested into KB | KB count increases after each insight generated |

> ⚠️ Insight generation requires AI (Azure OpenAI). Without AI configured, no insights are generated.

---

## 10. MCP with VS Code / Copilot CLI

### VS Code Configuration

The repo includes `.vscode/mcp.json` pre-configured:
```json
{
  "servers": {
    "ConferencePulse": {
      "type": "http",
      "url": "https://localhost:7231/mcp"
    }
  }
}
```

> ⚠️ If your port differs, update the URL. Get the actual port from the Aspire dashboard.

### Copilot CLI

```powershell
# In a terminal, tell Copilot about the MCP server
# (Copilot CLI auto-discovers tools from the MCP server)
# Then ask: "Summarize this conference session"
```

---

## 11. Full Demo Flow (End-to-End)

This simulates the actual live presentation:

```
SETUP:
  1. Open Aspire dashboard: http://localhost:18888
  2. Open 3 browser tabs from the web resource endpoint:
     - Tab 1: /presenter              (your laptop — speaker controls)
     - Tab 2: /display                (projector — audience-facing)
     - Tab 3: /session/DOTNETAI-CONF  (audience phone — participation)

ACT 1 — GO LIVE:
  3. Presenter: Click "🚀 Go Live"
     → Display updates from "Waiting..." to live session header
     → Session tab shows active session

ACT 2 — SEGMENT 1 (Microsoft.Extensions.AI):
  4. Presenter: Activate topic "meai"
     → Display shows active topic
  5. Presenter: Launch suggested poll "What's your experience level..."
     → Click "📡 Go Live" on the poll
  6. Session tab: Vote for an option
     → Display: bar chart updates live
     → Presenter: vote counts update
  7. Session tab: Submit a question
     → Presenter: question appears in "❓ Audience Questions"
     → Wait for AI auto-answer (🤖 badge appears)
     → Optionally override with human answer (✏️ Override)
  8. Presenter: Close poll
     → Wait for poll analysis insight (📊 appears in Insights panel)
     → KB count increases (poll results ingested)
  9. Presenter: Complete topic
     → Topic summary + knowledge gap insights auto-generated
     → KB count increases further

ACT 3 — SEGMENTS 2-4 (Knowledge, Agents, MCP):
  10. Repeat the activate → poll → vote → question → insight → complete cycle
      for "knowledge", "agents", and "mcp" topics
  11. Notice: polls get more contextual as KB grows (snowball effect)
  12. Notice: AI answers get richer with more context

ACT 4 — THE CLOSER:
  13. Activate topic "closer"
  14. Open Copilot CLI → point at MCP endpoint:
      "Summarize this conference session using the MCP server at <url>/mcp"
  15. Copilot calls generate_session_summary tool
      → Display shows cascade visualization lighting up
      → Summary includes all poll results, insights, questions, KB stats
```

---

## 12. Full Lifecycle Verification

Verify the complete data flow through one topic:

| # | Step | Verify |
|---|------|--------|
| 1 | Note initial KB count | ~20 from outline |
| 2 | Activate a topic | Topic shows as Active |
| 3 | Launch + activate poll | Poll visible on Session page |
| 4 | Vote from Session tab | Counts update on Presenter (real-time) |
| 5 | Submit a question | Question appears on Presenter (real-time) |
| 6 | Wait for AI answer | 🤖 AI badge + answer appear (real-time) |
| 7 | KB count increases | Q&A pair was ingested |
| 8 | Close poll | Poll closes; wait for insight |
| 9 | Poll insight appears | 📊 Poll Analysis in Insights panel |
| 10 | KB count increases | Poll results + insight ingested |
| 11 | Complete topic | Wait for topic insights |
| 12 | Topic insights appear | 📋 Topic Summary + 🔍 Knowledge Gap |
| 13 | KB count increases again | Topic insights ingested |
| 14 | MCP search covers new data | `search_session_knowledge` returns poll/Q&A/insight results |

---

## 13. Feature Matrix — What Works With/Without AI

| Feature | Without AI | With AI |
|---------|:----------:|:-------:|
| Session/topic management | ✅ | ✅ |
| Manual/suggested polls | ✅ | ✅ |
| Voting on polls | ✅ | ✅ |
| Submit/upvote questions | ✅ | ✅ |
| Answer questions (manual) | ✅ | ✅ |
| Poll results display | ✅ | ✅ |
| MCP tool discovery | ✅ | ✅ |
| MCP get_session_status | ✅ | ✅ |
| Aspire dashboard | ✅ | ✅ |
| **AI auto-answer questions** | ❌ | ✅ |
| **Auto-generate polls** | ❌ | ✅ |
| **Insight generation** | ❌ | ✅ |
| **Real-time ingestion** | ❌ | ✅ |
| **Session summary** | ❌ | ✅ |
| **Outline ingestion** | ❌ | ✅ |
| **Semantic search** | ⚠️ empty | ✅ |

---

## 14. Troubleshooting

| Symptom | Fix |
|---------|-----|
| `Aspire workload not found` | `dotnet workload install aspire` |
| `Parameter 'AzureOpenAIName' not found` | Set user secrets (see §0) |
| `openai` resource unhealthy | Check `az login` / resource name / RG in secrets |
| Web app port unknown | Get it from Aspire dashboard Resources tab |
| Outline ingestion skipped | AI not configured — check secrets + Azure login |
| MCP `initialize` fails | Make sure you POST to `<webUrl>/mcp` with correct JSON-RPC |
| Cross-tab updates don't appear | Blazor Server SignalR is per-circuit; refresh the other tab |
| `DefaultAzureCredential` errors | Run `az login` and ensure your account has Cognitive Services OpenAI User role on the resource |
| AI answer not appearing | Check structured logs in Aspire dashboard for `QuestionAnsweringService` errors |
| Insights not generating | Verify AI is configured; check logs for `InsightGenerationService` warnings |
| KB count not growing | Check structured logs for ingestion errors after poll close / topic complete |

---

## Quick Checklist

```
SETUP
[ ] .NET 10 SDK installed
[ ] Aspire workload installed
[ ] Azure OpenAI resource exists with "chat" + "embedding" deployments
[ ] User secrets set (AzureOpenAI:Name, AzureOpenAI:ResourceGroup)
[ ] az login completed

BUILD & LAUNCH
[ ] dotnet build — 0 errors, 0 warnings
[ ] aspire run (or dotnet run --project src/ConferenceAssistant.AppHost)
[ ] Aspire dashboard shows web + openai resources
[ ] Console shows "Session loaded: The Microsoft AI Stack for .NET"
[ ] Console shows "Ingested N outline chunks into knowledge base"

PAGES
[ ] Home (/) — 3 cards render and navigate
[ ] Presenter (/presenter) — dashboard loads, 5 topics visible
[ ] Session (/session/DOTNETAI-CONF) — shows waiting message
[ ] Display (/display) — shows waiting message

CORE FLOW
[ ] Presenter: Go Live → status changes to Live
[ ] Presenter: Activate topic → topic becomes Active
[ ] Presenter: Launch suggested poll → poll appears
[ ] Presenter: Activate poll → poll goes Live
[ ] Session: Vote on poll → count increments (real-time)
[ ] Display: Poll results update live
[ ] Session: Submit question → appears on Presenter (real-time)
[ ] Presenter: Close poll → results frozen

AI FEATURES (requires Azure OpenAI)
[ ] AI auto-answer → 🤖 AI answer appears within 5-10s of question
[ ] Presenter can override AI answer with ✏️ Override button
[ ] Close poll → 📊 Poll Analysis insight auto-generated
[ ] Complete topic → 📋 Topic Summary + 🔍 Knowledge Gap insights
[ ] KB count grows beyond initial 20 as events fire
[ ] Auto-generate poll → AI creates contextual poll
[ ] Knowledge search → returns ingested content (including poll/Q&A data)
[ ] MCP generate_session_summary → comprehensive output

MCP SERVER
[ ] POST /mcp initialize → returns server info
[ ] POST /mcp tools/list → 10 tools returned
[ ] POST /mcp tools/call get_session_status → session data
[ ] POST /mcp tools/call search_session_knowledge → KB results
[ ] .vscode/mcp.json configured for VS Code Copilot
```
