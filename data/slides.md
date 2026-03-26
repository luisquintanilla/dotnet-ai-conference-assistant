<!-- layout: centered -->

# The .NET Developer's Guide to AI Agents

## Building Agentic Apps with the Microsoft AI Stack

<!-- speaker:
Say: "Welcome everyone! Today we are going to walk through everything you need
to know to build AI agents in .NET -- from foundational abstractions to
production-ready patterns, multi-agent orchestration, and interoperability."

Timing: You have 60 minutes total. Keep this intro to ~2 minutes.
-->

---

<!-- layout: centered -->

## Scan to Join

### This is an interactive session

- Vote on polls throughout the talk
- Ask questions any time
- Your input shapes the conversation

<!-- speaker:
Point at the QR code on the display screen.
Say: "Take out your phones and scan the QR code. This session is interactive --
you will be voting on polls, asking questions, and helping us prioritize
what matters most to .NET developers building AI agents."
Wait 30-60 seconds for people to join.
-->

---

<!-- topic: ecosystem -->
<!-- layout: centered -->

# The .NET AI Ecosystem

## Abstractions That Actually Work

<!-- speaker:
Transition: "Let's start with the foundation -- the abstractions that make
everything else possible."
Timing: ~8 minutes for this segment.
Say: "How many of you have built against an AI provider SDK directly and then
had to switch providers? That pain is what Microsoft.Extensions.AI solves."
-->

---

## Key Abstractions

- **IChatClient** -- one interface for any LLM provider
- **IEmbeddingGenerator** -- provider-agnostic embeddings
- **ChatClientBuilder** -- composable middleware pipeline
- **AIFunctionFactory** -- turn any .NET method into an AI tool

<!-- speaker:
Walk through each abstraction briefly.
Say: "IChatClient is the core -- your code talks to this interface, not to
Azure OpenAI or Ollama or Anthropic directly. IEmbeddingGenerator follows
the same pattern for embeddings. ChatClientBuilder lets you compose
middleware like ASP.NET pipeline. And AIFunctionFactory bridges your
.NET methods into the AI tool-calling world."
-->

---

## The Middleware Pipeline

```csharp
var openaiBuilder = builder.AddAzureOpenAIClient("openai");
openaiBuilder.AddChatClient("chat")
    .UseFunctionInvocation()
    .UseOpenTelemetry()
    .UseLogging();
openaiBuilder.AddEmbeddingGenerator("embedding");
```

<!-- speaker:
Say: "This is real code. Three lines of middleware -- function invocation so
agents can call tools, OpenTelemetry so every LLM call shows up in your
distributed traces, and logging for debugging. The embedding generator
is registered separately for vector search. All of it is composable
and provider-agnostic."
-->

---

## Why It Matters

- **Provider-agnostic** -- swap Azure OpenAI for Ollama in one line
- **Composable** -- middleware for cross-cutting concerns
- **One interface** -- IChatClient works with any provider
- **Tool bridge** -- AI reasoning meets your .NET code

<!-- speaker:
Say: "Before M.E.AI, every provider had its own SDK. Switch providers,
rewrite your integration. M.E.AI eliminates that entirely. Write once,
run against any model."

Launch the first poll now.
Say: "Let's see where everyone is -- vote now on your experience level
with AI in .NET."
Wait for poll results. Comment on the distribution.
Timing check: you should be about 10 minutes in.
-->

---

<!-- topic: scenarios -->
<!-- layout: centered -->

# Scenarios and Decision Clarity

## Knowing What to Build

<!-- speaker:
Transition: "Now that you know the foundation, let's talk about the kinds of
things you can build -- and how to choose the right approach."
Timing: ~8 minutes for this segment.
-->

---

## Five Scenario Archetypes

1. **Background automation** -- scheduled tasks, data pipelines, no user interaction
2. **Interactive chat** -- conversational UI, streaming responses
3. **Tool-driven agent** -- LLM decides which tools to call and when
4. **Copilot extension** -- extend GitHub Copilot or M365 Copilot
5. **Experimentation** -- prototyping, evaluating models, learning

<!-- speaker:
Say: "Every AI project falls into one of these five buckets. Background
automation is the simplest -- think scheduled jobs that use an LLM.
Interactive chat adds streaming and conversation. Tool-driven agents
are where it gets interesting -- the model decides what to do.
Copilot extensions let you plug into existing AI surfaces.
And experimentation is where most of you should start."
-->

---

## RAG and Tool Calling

- **RAG** -- Retrieval-Augmented Generation gives the model context
- **Tool calling** -- the model invokes your code to take action
- Together they form the **agent loop**: reason, retrieve, act, repeat
- Most real-world agents combine both patterns

<!-- speaker:
Say: "RAG gives the model knowledge it does not have. Tool calling gives the
model hands to act with. An agent combines both -- it reasons about what
it needs, retrieves relevant context, calls tools to take action, and
repeats until the task is done. This is the core loop of agentic AI."
-->

---

## The Decision Framework

| Need | Use |
|------|-----|
| Simple LLM calls, middleware | **M.E.AI (IChatClient)** |
| Agents, tools, multi-agent orchestration | **Microsoft Agent Framework** |
| Cross-app tool interop | **Model Context Protocol (MCP)** |

<!-- speaker:
Say: "Here is the decision framework. If you just need to call an LLM with
some middleware, use M.E.AI directly. If you need agents with tools and
multi-agent patterns, use the Microsoft Agent Framework -- it builds on
M.E.AI. If you need your tools to be consumable by other AI apps, add MCP."

Launch the scenario poll now.
Say: "Which scenario are you most likely to build first? Vote now."
Wait for results and comment briefly.
Timing check: you should be about 18 minutes in.
-->

---

<!-- topic: frameworks -->
<!-- layout: centered -->

# Agent Frameworks and Value

## From Abstractions to Agents

<!-- speaker:
Transition: "You know the abstractions. You know the scenarios. Now let's
build an actual agent."
Timing: ~10 minutes for this segment.
-->

---

## ChatClientAgent

```csharp
ChatClientAgent agent = new(
    chatClient,
    name: "ResearchAssistant",
    instructions: "You are a research assistant...",
    tools: [searchTool, summarizeTool]);
```

- Built on **IChatClient** -- same abstraction you already know
- **Instructions** define persona and behavior
- **Tools** are registered via AIFunctionFactory
- **Structured output** for type-safe responses

<!-- speaker:
Say: "Creating an agent is this simple. Give it a chat client, a name,
instructions that define its behavior, and tools it can call.
ChatClientAgent wraps IChatClient -- it is not a new SDK. It is
the same abstractions with agent capabilities layered on top."
-->

---

## Multi-Agent Orchestration

- **Sequential** -- agents run in order, each building on the last
- **Concurrent** -- fan-out to multiple agents, merge results
- **Handoff** -- one agent decides which agent should go next

<!-- speaker:
Say: "Single agents are useful, but real-world scenarios often need
multiple agents working together. Sequential is like a relay race.
Concurrent fans out to multiple specialists and merges their outputs.
Handoff is the most powerful -- one agent triages and routes to the
right specialist. Think of a customer service system where a router
agent sends you to billing, technical support, or sales."
-->

---

## Framework Value vs. Friction

- **Value**: agent loop, tool dispatch, conversation management, orchestration
- **Friction**: learning curve, abstraction overhead, debugging complexity
- The sweet spot: use the framework for what it gives you, drop down to
  IChatClient when the framework gets in the way

<!-- speaker:
Say: "Be honest about the tradeoffs. Frameworks give you a lot -- the agent
loop, tool dispatch, multi-agent orchestration. But they also add
abstraction layers that can make debugging harder. The key is knowing
when to use the framework and when to drop down to raw IChatClient.
Not every problem needs a multi-agent system."

Launch the framework value poll now.
Say: "What would make agent frameworks more valuable to you? Vote now."
Wait for results.
Timing check: you should be about 28 minutes in.
-->

---

<!-- topic: production -->
<!-- layout: centered -->

# Production Readiness

## Closing the Confidence Gap

<!-- speaker:
Transition: "Building an agent demo is easy. Shipping one to production is
a different story. Let's talk about what it takes."
Timing: ~10 minutes for this segment.
-->

---

## OpenTelemetry Integration

```csharp
openaiBuilder.AddChatClient("chat")
    .UseFunctionInvocation()
    .UseOpenTelemetry(configure =>
        configure.EnableSensitiveData = true)
    .UseLogging();
```

- Every LLM call becomes a **span** in your distributed traces
- Token usage, latency, and model info captured automatically
- Plug into **any** OpenTelemetry-compatible backend

<!-- speaker:
Say: "Observability is not optional in production. This middleware captures
every LLM call as a span -- token counts, latency, model name, even
prompt and completion text if you enable sensitive data. It flows into
whatever OpenTelemetry backend you already use -- Aspire Dashboard,
Jaeger, Application Insights, Grafana."
-->

---

## The Five Production Blockers

1. **Reliability** -- LLMs are non-deterministic; retries, fallbacks, guardrails
2. **Observability** -- you cannot fix what you cannot see
3. **Security** -- prompt injection, data leakage, tool authorization
4. **Cost** -- token usage adds up fast at scale
5. **Patterns** -- no established best practices yet

<!-- speaker:
Say: "These are the five things that keep AI apps out of production.
Reliability -- LLMs sometimes return garbage and you need retries and
fallbacks. Observability -- you need to see every call. Security --
prompt injection is real, and tool authorization matters. Cost --
tokens are not free, especially at scale. And patterns -- we do not
have decades of best practices like we do for web apps."
-->

---

## The Confidence Gap

- **Demos work** -- controlled inputs, happy paths, small scale
- **Production is different** -- adversarial inputs, edge cases, cost pressure
- The gap: most teams can build a demo but hesitate to ship
- Closing it requires: testing, observability, guardrails, and iteration

<!-- speaker:
Say: "Here is the uncomfortable truth. Almost every team I talk to can build
an impressive AI demo in a week. But when you ask them to ship it to
production, they hesitate. The gap between demo and production is real,
and closing it takes investment in testing, observability, and guardrails."

Launch the production readiness poll now.
Say: "What is your biggest blocker to shipping AI in production? Vote now."
Wait for results. Comment on what the audience finds most challenging.
Timing check: you should be about 38 minutes in.
-->

---

<!-- topic: interop -->
<!-- layout: centered -->

# Ecosystem and Interoperability

## Making .NET a First-Class AI Citizen

<!-- speaker:
Transition: "We have talked about building agents. Now let's talk about
connecting them to the broader AI ecosystem."
Timing: ~8 minutes for this segment.
-->

---

## MCP Server in .NET

```csharp
builder.Services
    .AddMcpServer(options => {
        options.ServerInfo = new() {
            Name = "MyAgent", Version = "1.0.0"
        };
    })
    .WithToolsFromAssembly()
    .WithHttpTransport();
app.MapMcp("/mcp");
```

<!-- speaker:
Say: "Model Context Protocol lets any AI app consume your tools. AddMcpServer,
discover tools from the assembly, enable HTTP transport, map the endpoint.
That is it. GitHub Copilot, VS Code, Claude Desktop, any MCP client can
now call your tools. Your .NET app just became part of the AI ecosystem."
-->

---

## .NET's Strategic Positioning

1. **Best runtime for AI backends** -- performance, memory safety, enterprise readiness
2. **Full-stack AI platform** -- from abstractions to agents to interop
3. **Enterprise bridge** -- connecting existing .NET systems to AI capabilities
4. **Cross-platform agent host** -- run agents on any OS, any cloud

<!-- speaker:
Say: ".NET has four strategic positions in the AI space. It is the best runtime
for building AI backend services. It provides a full-stack platform from
low-level abstractions to high-level agent orchestration. It bridges your
existing enterprise .NET systems to AI. And it runs everywhere -- any OS,
any cloud, any container runtime."
-->

---

## Cross-Framework Interop

- **MCP** connects .NET agents to Python, TypeScript, and other ecosystems
- **IChatClient** abstracts the model -- your agent code does not care what is behind it
- **OpenTelemetry** gives you a unified view across languages and frameworks
- The vision: polyglot AI systems that use the best tool for each job

<!-- speaker:
Say: "The AI ecosystem is polyglot. Python has the best ML libraries. TypeScript
has the best browser integrations. .NET has the best enterprise runtime.
MCP, IChatClient, and OpenTelemetry let you build systems that span all
of them without tight coupling."

Launch the ecosystem poll now.
Say: "What interop scenario matters most to you? Vote now."
Wait for results.
Timing check: you should be about 46 minutes in.
-->

---

<!-- topic: priorities -->
<!-- layout: centered -->

# Priorities and Next Steps

## What .NET Developers Need Most

<!-- speaker:
Transition: "We have covered the stack from top to bottom. Now let's talk about
what comes next -- and what you think should be prioritized."
Timing: ~10 minutes for the rest of the session.
-->

---

## Force-Rank the Investment Areas

1. **Guidance** -- reference architectures, best practices, decision frameworks
2. **Templates** -- dotnet new templates for common agent scenarios
3. **Tooling** -- IDE integration, debugging, prompt testing
4. **Simplification** -- reduce boilerplate, improve defaults
5. **Smart defaults** -- opinionated starters that just work

<!-- speaker:
Say: "If you could only invest in one of these, which would it be? Guidance
means documentation and architectural patterns. Templates means
dotnet new agent-chat and you have a working app. Tooling means better
debugging and prompt testing in Visual Studio. Simplification means
fewer lines of code to get started. Smart defaults means opinionated
choices so you do not have to make every decision yourself."
-->

---

## Adoption Accelerators

- **Reference apps** -- real-world examples, not toy demos
- **Architectural guidance** -- when to use what, and why
- **IDE integration** -- debugging agents in Visual Studio and VS Code
- **Cost controls** -- token budgets, caching, model routing

<!-- speaker:
Say: "These are the things that will actually accelerate adoption. Reference
apps that show real patterns, not just hello-world demos. Architectural
guidance so teams do not have to figure out the hard problems alone.
IDE integration so debugging an agent is as natural as debugging a
web app. And cost controls so you can ship without worrying about
a surprise bill."

Launch the priorities poll now.
Say: "What would accelerate your AI adoption the most? Vote now."
Wait for results. This is the last poll -- spend a moment discussing
what the audience prioritized.
Timing check: you should be about 53 minutes in.
-->

---

## Resources

- **The .NET Developer's Guide to AI Agents** -- github.com/JeremyLikness/dotnet-developer-guide-ai-agents
- **Microsoft.Extensions.AI** -- github.com/dotnet/extensions
- **Microsoft Agent Framework** -- github.com/microsoft/agent-framework
- **MCP for .NET** -- github.com/modelcontextprotocol/csharp-sdk
- **VectorData** -- learn.microsoft.com/dotnet/ai

<!-- speaker:
Say: "All the links are on screen. The Developer's Guide repo has the full
companion content for this talk. The Extensions repo has M.E.AI.
The Agent Framework repo has ChatClientAgent and orchestration.
The MCP C# SDK has everything you need for Model Context Protocol.
And the VectorData docs on learn.microsoft.com cover semantic search."
Pause for people to take photos of the screen.
-->

---

<!-- layout: centered -->

# Thank You

### The .NET Developer's Guide to AI Agents

<!-- speaker:
Say: "Thank you for being part of this session. The AI agent space in .NET is
moving fast -- the abstractions are solid, the frameworks are maturing,
and the interop story is coming together. Go build something, share what
you learn, and help shape where this ecosystem goes next."

If time permits, take 2-3 live questions from the audience.
Timing: you should finish right around 60 minutes.
-->
