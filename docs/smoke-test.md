# 🧪 Conference Pulse — Smoke Test & Getting Started Guide

> **Updated for multi-session architecture + Aspire AI integration + real-time ingestion + AI answers + insight generation**
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

# --- Azure local provisioning (required by Aspire for any Azure resource) ---
dotnet user-secrets set "Azure:SubscriptionId" "<your-azure-subscription-id>"
dotnet user-secrets set "Azure:Location" "eastus"

# --- Azure OpenAI resource reference ---
# Your Azure OpenAI resource name (not the full URL — just the name)
dotnet user-secrets set "AzureOpenAI:Name" "my-openai-resource"

# The resource group it lives in
dotnet user-secrets set "AzureOpenAI:ResourceGroup" "my-resource-group"
```

> 💡 **Find your subscription ID:** `az account show --query id -o tsv`

| Secret | Description | Example |
|--------|-------------|---------|
| `Azure:SubscriptionId` | Your Azure subscription ID (Aspire local provisioning) | `12345678-abcd-...` |
| `Azure:Location` | Azure region for provisioned resources | `eastus` |
| `AzureOpenAI:Name` | Your Azure OpenAI resource name | `my-openai-resource` |
| `AzureOpenAI:ResourceGroup` | Resource group containing the AOAI resource | `my-rg` |

Aspire reads the `Azure:*` secrets for local provisioning context, and the
`AzureOpenAI:*` secrets via `AddParameterFromConfiguration` → `RunAsExisting`
→ resolves the endpoint → injects the connection string into the web project
automatically.

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

# One-time: log in to dev tunnels
devtunnel user login

# Launch via Aspire CLI (recommended) or project
aspire run
# OR: dotnet run --project src/ConferenceAssistant.AppHost
```

### ✅ What you should see

```
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 13.2.0
      Dashboard is running at: http://localhost:18888/login?t=<token>
      ...
info: Dev tunnel 'conference-tunnel' is running at: https://<id>.devtunnels.ms/
      Default session created: XXXXXXXX (PIN: 0000)
      Session loaded: The Microsoft AI Stack for .NET
      Ingested 12 outline chunks into knowledge base
```

> 📌 **Note the session code** in the startup logs (e.g., `XXXXXXXX`). The default
> demo session is auto-created with PIN **`0000`** and an auto-generated session code.
> You'll need this code to access presenter, display, and attendee views.

### Key URLs

| URL | What |
|-----|------|
| **Aspire Dashboard** | `http://localhost:18888` (shown in console output) |
| **Web App** | Check the dashboard → `web` resource → click the endpoint (typically `https://localhost:<port>`) |
| **Home / Sessions** | `/` — Landing page showing active sessions + "Create New Session" button |

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

The homepage serves as the **session hub** — it lists all active sessions and lets you create new ones.

| # | Check | Expected |
|---|-------|----------|
| 1 | Page loads | Conference Pulse landing page appears |
| 2 | Active sessions list | Default session visible (auto-created at startup) |
| 3 | Session cards | Each shows title, session code, status, and attendee count |
| 4 | "Create New Session" button | Visible and navigates to `/create` |
| 5 | Click a session card | Expands/shows join options (Presenter, Attendee, Display) |
| 6 | Click "Presenter Dashboard" | Navigates to `/presenter/{SessionCode}` |
| 7 | Click "Join Session" | Navigates to `/session/{SessionCode}` |
| 8 | Click "Projection Display" | Navigates to `/display/{SessionCode}` |

---

## 4. Presenter Dashboard — `/presenter/{SessionCode}`

Open this in your "speaker laptop" browser window.

> 🔐 **PIN Gate:** Navigating to `/presenter/{SessionCode}` first shows a PIN input.
> Enter the host PIN (default session uses `0000`) to unlock the full dashboard.
> PIN validation is per Blazor circuit (browser tab) — no cookies or tokens.

The presenter uses a **3-column layout** (simultaneous, no tabs):
- **Left column** (~220px) — Topic outline: auto-highlights current topic based on slide's TopicId, click-to-jump navigation, collapsible import section
- **Center column** (flex) — Current slide preview, up-next preview, speaker notes, slide navigation, quick poll launch
- **Right column** (~320px) — Global Q&A feed (all topics), active poll results, insights

### 4a. Session Setup State

| # | Check | Expected |
|---|-------|----------|
| 1 | Session title shows | "The Microsoft AI Stack for .NET" |
| 2 | Status badge | Shows "Setup" |
| 3 | Knowledge base counter | Shows "📚 X records" (X > 0 if AI configured) |
| 4 | 5 topics in left column | meai, knowledge, agents, mcp, closer |
| 5 | "🚀 Go Live" button visible | Yes |
| 6 | Topic activate buttons | Should NOT appear (session not live yet) |
| 7 | 3-column layout | Left: topic outline, Center: slide zone, Right: Q&A + polls + insights |

### 4b. Go Live

| # | Action | Expected |
|---|--------|----------|
| 1 | Click **🚀 Go Live** | Status badge changes to "Live" |
| 2 | Button changes | Now shows "⏹ End Session" |
| 3 | Topics | Each shows **▶ Activate** button |

### 4c. Topic Lifecycle

| # | Action | Expected |
|---|--------|----------|
| 1 | Click **▶ Activate** on "Microsoft.Extensions.AI" | Topic becomes active, left column highlights it |
| 2 | Active topic badge | Shows "Active" status |
| 3 | Other topics | Still show "Upcoming" |
| 4 | Click **✓ Complete** on active topic | Status changes to "Completed" |
| 5 | Activate next topic | New topic becomes active, previous stays completed |
| 6 | Navigate slides past topic boundary | Topic auto-activates via SyncTopicToSlide (left column updates) |

### 4d. Polls — Suggested

| # | Action | Expected |
|---|--------|----------|
| 1 | Activate topic "meai" | Poll section appears in center column |
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
| 4 | Create poll with no active topic | Poll created as session-level (TopicId is null) |

---

## 5. Attendee Session — `/session/{SessionCode}`

Open in a second browser window (or phone). Replace `{SessionCode}` with the
code from the startup logs (e.g., `/session/XXXXXXXX`).

### 5a. Before Go Live

| # | Check | Expected |
|---|-------|----------|
| 1 | Navigate to page | "⏳ Session hasn't started yet. Hang tight!" |

### 5b. Voting (after Go Live + active poll)

| # | Action | Expected |
|---|--------|----------|
| 1 | Click a poll option | Vote registered |
| 2 | Results update | Vote count increments |
| 3 | Switch to Presenter | Presenter sees updated counts + percentages in right column |

### 5c. Questions + AI Auto-Answer

| # | Action | Expected |
|---|--------|----------|
| 1 | Type in "Ask a Question" box | Text appears |
| 2 | Click **Send** | Question submitted, input clears |
| 3 | Question in "🔥 Top Questions" | With 👍 0 count |
| 4 | Click 👍 on a question | Count increments |
| 5 | Switch to Presenter | Question visible in right column "❓ Audience Questions" (global — all topics) |
| 6 | Wait 5-10 seconds | 🤖 AI answer appears automatically (blue-tinted, with AI badge) |
| 7 | AI answer uses KB context | Answer references session outline content |
| 8 | Topic badge on question | Shows which topic the question came from |

### 5d. Answering / Overriding (from Presenter)

| # | Action | Expected |
|---|--------|----------|
| 1 | Question has no AI answer yet | **💬 Answer** button shows → type answer + submit |
| 2 | Question already has AI answer | **✏️ Override** button shows below the AI answer |
| 3 | Click **✏️ Override** | Text input appears to replace AI answer |
| 4 | Type human answer + Submit | AI answer replaced with human answer (💬 badge, no AI badge) |

---

## 6. Projection Display — `/display/{SessionCode}`

Open in a third browser window (simulates projector/big screen).

| # | Check | Expected |
|---|-------|----------|
| 1 | Before Go Live | Large QR code with "Scan to Join" + session code + URL |
| 2 | After Go Live | Session title + active topic in header; sidebar QR code visible |
| 3 | When poll active | PollResultsChart renders with live bar chart |
| 4 | Vote from Session tab | Display updates with new vote counts |
| 5 | When no active poll/slide | Shows QR code (idle state) |
| 6 | When slide active | Current slide full-screen; smaller QR code in sidebar |

---

## 7. Multi-Session Testing

The app supports multiple concurrent sessions. Each session has its own code, host PIN, and isolated state.

### 7a. Create a New Session

| # | Action | Expected |
|---|--------|----------|
| 1 | Navigate to `/` (home page) | Active sessions list visible |
| 2 | Click **"Create New Session"** | Navigates to `/create` |
| 3 | Fill in title (e.g., "My Test Session") | Title field accepts input |
| 4 | Optionally set a custom session code | Auto-generated if left blank |
| 5 | Set a host PIN (4-6 digits) | Required field |
| 6 | Optionally add description and template | Optional fields |
| 7 | Click **Create** | Redirects to home page; new session appears in list |

### 7b. Join as Attendee

| # | Action | Expected |
|---|--------|----------|
| 1 | From home page, find your new session | Session card visible |
| 2 | Click **"Join Session"** (or navigate to `/session/{YourCode}`) | Attendee view loads for that session |
| 3 | Session state is isolated | Polls, questions, topics are specific to this session |

### 7c. Open Presenter View with PIN

| # | Action | Expected |
|---|--------|----------|
| 1 | Navigate to `/presenter/{YourCode}` | PIN gate appears — input field for host PIN |
| 2 | Enter incorrect PIN | Error message, dashboard stays locked |
| 3 | Enter correct PIN | Full presenter dashboard unlocks |
| 4 | PIN persists per browser tab | Refreshing the tab does NOT re-prompt (same Blazor circuit) |
| 5 | Open a new tab to same URL | PIN gate appears again (new circuit) |

### 7d. Open Display View

| # | Action | Expected |
|---|--------|----------|
| 1 | Navigate to `/display/{YourCode}` | Projection view loads for that session |
| 2 | Display is session-specific | Shows only polls/slides/insights for that session code |

### 7e. Concurrent Sessions

| # | Action | Expected |
|---|--------|----------|
| 1 | Create two sessions with different codes | Both appear on home page |
| 2 | Open presenter for each in separate tabs | Each has independent state |
| 3 | Launch a poll in session A | Only session A attendees/display see the poll |
| 4 | Submit a question in session B | Only session B presenter sees the question |

---

## 8. MCP Server — `/mcp`

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

## 9. Real-Time Ingestion — Knowledge Base Growth

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

## 10. Insight Generation — Auto-Analysis

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

## 11. MCP with VS Code / Copilot CLI

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

## 12. Full Demo Flow (End-to-End)

This simulates the actual live presentation:

```
SETUP:
  1. Open Aspire dashboard: http://localhost:18888
  2. Note the default session code from startup logs (e.g., XXXXXXXX)
  3. Open 3 browser tabs from the web resource endpoint:
     - Tab 1: /presenter/{SessionCode}   (your laptop — speaker controls)
       → Enter PIN "0000" when prompted
     - Tab 2: /display/{SessionCode}     (projector — audience-facing)
     - Tab 3: /session/{SessionCode}     (audience phone — participation)

ACT 1 — GO LIVE:
  3. Presenter: Click "🚀 Go Live"
     → Display updates from QR code idle state to live session header
     → QR code moves to sidebar; session title visible
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
      → Display shows summary content with QR code in sidebar
      → Summary includes all poll results, insights, questions, KB stats
```

---

## 13. Full Lifecycle Verification

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

## 14. Feature Matrix — What Works With/Without AI

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

## 15. Troubleshooting

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
| PIN gate not appearing | Ensure you're navigating to `/presenter/{SessionCode}` (not just `/presenter`) |
| Session code unknown | Check startup logs for "Default session created: XXXXXXXX (PIN: 0000)" |
| Session not on home page | Refresh `/` — session list is loaded from `SessionManager` |

---

## 16. Slide System

### Verify Slides Load
```
# Check the startup logs in the Aspire dashboard or terminal
# Should see: "Slides loaded: 28 slides"
```

### Presenter Slide Navigation
1. Navigate to `/presenter/{SessionCode}` and enter the host PIN
2. Click **Go Live** to start the session
3. Activate the first topic (Microsoft.Extensions.AI)
4. ✅ The center column should show a **slide preview** with the topic's first slide
5. ✅ **Speaker notes** should appear below the preview (with 🎤 icon)
6. Click **Next ▶** — slide advances, preview and notes update
7. Click **◀ Previous** — slide goes back
8. ✅ Progress shows "Slide X of Y"
9. ✅ "Up Next" preview shows the next slide
10. ✅ Left column topic outline auto-highlights the current topic based on slide's TopicId

### Three-Column Layout Verification
1. After verifying slides work, confirm all three columns are visible simultaneously
2. ✅ **Left column** (~220px): Topic outline with current topic highlighted
3. ✅ **Center column** (flex): Slide preview, up-next, speaker notes, navigation, quick poll launch
4. ✅ **Right column** (~320px): Global Q&A feed (questions from all topics with topic badges), active poll results, insights
5. ✅ Clicking a topic in the left column jumps to that topic's first slide
6. ✅ Navigating slides past a topic boundary auto-activates the new topic (SyncTopicToSlide)
7. ✅ Import section in the left column is collapsible

### Display Slide Rendering
1. Open `/display/{SessionCode}` in a separate browser window
2. ✅ When idle (no poll/slide active), the display shows a **large QR code** with "Scan to Join" + session code + URL
3. ✅ When a slide is active, the slide shows full-screen with a smaller QR code in the sidebar
4. ✅ Large text, dark background, readable from back of room
5. ✅ Progress dots at the bottom show current position
6. Advance a slide on the presenter → ✅ display updates in real-time

### Keyboard Navigation
1. Click in the center column area on the presenter page (to focus it)
2. Press **→** (right arrow) → slide advances
3. Press **←** (left arrow) → slide goes back
4. Press **Space** → slide advances
5. Press **P** → quick-launch poll
6. Press **Esc** → close active overlay
7. ✅ Keyboard navigation does NOT trigger when typing in a text input (poll question, answer form)

### Poll/Slide Priority
1. While a slide is showing on the display, launch a poll
2. ✅ The poll **replaces** the slide on the display
3. Close the poll
4. ✅ The slide **returns** on the display

### Topic Auto-Navigation
1. Activate a different topic (e.g., "Knowledge Engineering") on the presenter
2. ✅ The slide automatically jumps to the **first slide** of that topic
3. ✅ Both presenter and display update accordingly
4. Navigate slides forward past a topic boundary (without clicking a topic)
5. ✅ The topic auto-activates via SyncTopicToSlide — left column highlights the new topic

### Speaker Notes Privacy
1. While slides are showing, compare `/presenter/{SessionCode}` and `/display/{SessionCode}`
2. ✅ Speaker notes (timing cues, demo instructions) appear **only** on the presenter
3. ✅ The display shows **only** the slide content (no notes)

---

## 17. GitHub Repository Import

### Creating a Session from GitHub Content

1. Navigate to the home page (`/`)
2. Click **"🎯 Create a Session"**
3. Enter session details (title, code, PIN)
4. Select **"🐙 Import from GitHub"** template
5. Enter a GitHub repo URL: `https://github.com/JeremyLikness/dotnet-ai-scenarios`
6. Click **"🔍 Fetch & Draft Session"**
7. Wait for import + AI drafting (may take 10-30 seconds)
8. ✅ Verify: Import stats show document count
9. ✅ Verify: AI-drafted topics appear with talking points and suggested polls
10. Optionally remove topics using ✕ button
11. Click **"🚀 Create Session"**
12. ✅ Verify: Redirected to presenter page with imported topics
13. ✅ Verify: Center column in Presenter shows generated slides
14. ✅ Verify: Display page (`/display/{code}`) shows slides when navigating
15. ✅ Verify: Slide types include Title, Section, Content, and Poll slides
16. ✅ Verify: Speaker notes appear for each slide in the Presenter view

### Importing Content During a Session

1. Open presenter dashboard for an active session
2. Expand the **"📥 Import"** section in the left column
3. Paste a GitHub repo URL
4. Click **"📥 Import Repository"**
5. ✅ Verify: Import completes, history entry appears
6. ✅ Verify: Knowledge base record count increases
7. Click **"➕ Add All Topics"** to add drafted topics to the session
8. ✅ Verify: New topics appear in the Topics panel

### Edge Cases

- **Invalid URL**: Should show error message
- **Rate limit**: GitHub API allows 60 requests/hour unauthenticated
- **AI unavailable**: Fallback generates topics grouped by category from front matter

### Slide Generation

1. Import a GitHub repo via the Create Session page
2. After AI drafting completes, create the session
3. Open the Presenter dashboard → center column shows slides
4. ✅ Verify: Title slide shows session name
5. ✅ Verify: Section slides for each topic
6. ✅ Verify: Content slides with talking points as bullets
7. ✅ Verify: Poll slides for topics with suggested polls
8. ✅ Verify: Closing "Thank You" slide
9. Navigate through slides using Next/Prev buttons
10. ✅ Verify: Display page updates in sync with slide navigation

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
[ ] Console shows "Default session created: XXXXXXXX (PIN: 0000)"
[ ] Console shows "Session loaded: The Microsoft AI Stack for .NET"
[ ] Console shows "Ingested N outline chunks into knowledge base"

PAGES
[ ] Home (/) — session hub: lists active sessions + "Create New Session" button
[ ] Presenter (/presenter/{code}) — PIN gate prompts for host PIN, then unlocks dashboard
[ ] Presenter dashboard — 3-column layout: topic outline (left) + slides/notes (center) + Q&A/polls/insights (right)
[ ] All three columns visible simultaneously — no tabs
[ ] Display (/display/{code}) — shows QR code "Scan to Join" before Go Live

MULTI-SESSION
[ ] Home page lists default session
[ ] /create — create new session with custom code + PIN
[ ] New session appears on home page
[ ] Presenter PIN gate works (wrong PIN rejected, correct PIN unlocks)
[ ] Sessions are isolated (polls/questions don't cross over)

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

GITHUB REPOSITORY IMPORT
[ ] /create — "🐙 Import from GitHub" template available
[ ] Enter GitHub repo URL → "🔍 Fetch & Draft Session" imports and drafts
[ ] Import stats show document count
[ ] AI-drafted topics appear with talking points and polls
[ ] "🚀 Create Session" → redirected to presenter with imported topics
[ ] Presenter "📥 Import" section (left column) — paste URL → "📥 Import Repository" works
[ ] KB record count increases after import
[ ] "➕ Add All Topics" adds drafted topics to session
```
