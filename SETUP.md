# Setup: "The .NET Developer's Guide to AI Agents" Session

This guide explains how to set up the Conference Pulse app with the pre-built "AI Agents in .NET" session, including 12 curated polls and automatic knowledge base import from the Jeremy Likness guide.

## Quick Setup (Drop-in Files)

The three data files are **drop-in replacements**. Copy them into the `data/` directory:

| File | Purpose |
|------|---------|
| `data/seed-topics.json` | Session definition with 6 topics and 12 polls |
| `data/session-outline.md` | Session outline for knowledge base ingestion |
| `data/slides.md` | Slide deck with speaker notes (27 slides) |

These files are already in place if you're on the `feature/ai-agents-session` branch.

## Program.cs Changes

Two changes are needed in `src/ConferenceAssistant.Web/Program.cs`:

### 1. Update the default session code (~line 407)

**Find this:**
```csharp
if (sessionManager.GetSession("DOTNETAI-CONF") is null)
```

**Replace with:**
```csharp
const string defaultSessionCode = "AGENTS-GUIDE";
if (sessionManager.GetSession(defaultSessionCode) is null)
```

And update the `else` branch (~line 418):
```csharp
sessionService.SetDefaultSession(defaultSessionCode);
```

### 2. Add auto-import of the GitHub repo (~after line 449, after the MCP client init block)

Add this block after the MCP client initialization `Task.Run`:

```csharp
// Auto-import the .NET Developer's Guide to AI Agents into the knowledge base
_ = Task.Run(async () =>
{
    try
    {
        var ingestion = app.Services.GetRequiredService<IIngestionService>();
        var result = await ingestion.IngestGitHubRepoAsync("JeremyLikness", "dotnet-developer-guide-ai-agents");
        app.Logger.LogInformation(
            "Auto-imported knowledge base: {Count} documents from JeremyLikness/dotnet-developer-guide-ai-agents ({Errors} errors)",
            result.RecordCount, result.Errors.Count);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "GitHub repo auto-import failed — knowledge base will be populated manually");
    }
});
```

## Copilot Prompt (Paste This)

If you'd rather have Copilot apply the `Program.cs` changes for you, paste this prompt:

> In `src/ConferenceAssistant.Web/Program.cs`, make two changes:
> 1. Change the default session code from `"DOTNETAI-CONF"` to `"AGENTS-GUIDE"`. Extract it to a `const string defaultSessionCode` and use it in both the `GetSession()` check and the `SetDefaultSession()` call.
> 2. After the MCP client initialization `Task.Run` block, add a new `Task.Run` that calls `IIngestionService.IngestGitHubRepoAsync("JeremyLikness", "dotnet-developer-guide-ai-agents")` to auto-import the guide into the knowledge base at startup. Wrap it in try/catch, log success with record count, log warning on failure.

## Session Details

- **Session code:** `AGENTS-GUIDE`
- **PIN:** `0000` (default)
- **Topics:** 6 (ecosystem, scenarios, frameworks, production, interop, priorities)
- **Polls:** 12 (2 per topic, single-choice, curated for positioning decisions)
- **Knowledge base:** Auto-imported from [JeremyLikness/dotnet-developer-guide-ai-agents](https://github.com/JeremyLikness/dotnet-developer-guide-ai-agents)

## What Happens at Startup

1. Session loads from `seed-topics.json` with 6 topics and 12 suggested polls
2. Slides load from `slides.md` (27 slides with speaker notes)
3. Session outline is ingested into the knowledge base
4. MCP clients connect to Microsoft Learn + DeepWiki
5. The Jeremy Likness guide repo is auto-imported into the knowledge base (async, non-blocking)
6. AI agents have full context for answering questions and generating polls
