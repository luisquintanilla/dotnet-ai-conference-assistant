# The .NET Developer's Guide to AI Agents — From Primitives to Production

## Introduction: Where .NET Meets AI

The AI landscape for .NET developers has exploded. Between Microsoft.Extensions.AI, the Microsoft Agent Framework, Model Context Protocol, and a growing constellation of libraries and patterns, there's never been more capability available — or more decisions to make. This session cuts through the noise. We'll walk through the full spectrum of AI development in .NET, from the foundational abstractions you'll use in every project to multi-agent orchestration and production deployment. Whether you're adding a smart feature to an existing app or building an agent-first system, you'll leave with a clear mental model of what to use and when.

## Segment 1: The .NET AI Ecosystem

The .NET AI ecosystem has matured rapidly, and most developers are entering it from one of a few starting points: calling an LLM via the OpenAI SDK, experimenting with Semantic Kernel, or exploring the newer Microsoft.Extensions.AI (MEAI) abstractions. Understanding where these pieces fit — and how they relate — is the first step toward building with confidence.

At the foundation sits Microsoft.Extensions.AI, a set of provider-agnostic abstractions that give .NET developers a common language for AI integration. MEAI doesn't replace higher-level frameworks; it underpins them. Whether you're building a simple chat feature or a multi-agent system, MEAI's interfaces are the layer your code talks to.

### Core Abstractions

- **IChatClient** — The unified interface for chat completions. It works with Azure OpenAI, OpenAI, Ollama, and any compatible provider. Your application code targets `IChatClient`, not a specific vendor SDK, making provider switches a configuration change rather than a rewrite.
- **IEmbeddingGenerator<string, Embedding<float>>** — The same provider-agnostic pattern applied to text embeddings. Generate vectors for semantic search without coupling to a specific embedding service.
- **ChatClientBuilder** — A composable middleware pipeline for cross-cutting concerns. Chain function invocation, OpenTelemetry tracing, rate limiting, and caching as middleware — the same pattern .NET developers know from ASP.NET Core.
- **AIFunctionFactory.Create()** — Turn any .NET method into a tool that AI models can call. This is the bridge between AI reasoning and your existing code. Annotate a method, register it, and the model can invoke it with structured arguments.

### The Spectrum of Choices

The ecosystem spans a wide range: raw HTTP calls to model APIs at one end, full agent frameworks at the other. In between, MEAI provides the primitives, Semantic Kernel adds planners and plugins, and the Microsoft Agent Framework offers agent lifecycle management. The key insight is that these aren't competing choices — they're layers. Most production applications will use more than one.

## Segment 2: Scenarios and Decision Clarity

Not every AI workload needs an agent. One of the most valuable skills a .NET developer can build right now is matching workloads to the right level of abstraction. Getting this wrong means either over-engineering a simple feature or under-building a system that needs autonomy.

The practical breakdown comes down to three broad patterns. Background automation — summarization, classification, extraction — typically needs only MEAI with a well-crafted prompt and maybe some structured output. Interactive chat experiences benefit from conversation history management and streaming, which frameworks handle well. Tool-driven agents, where the model decides which actions to take and in what order, are where the Microsoft Agent Framework and multi-step orchestration earn their keep.

### RAG for Grounding

- **Retrieval-Augmented Generation (RAG)** is the pattern for giving models access to your data without fine-tuning. Chunk your documents, generate embeddings, store them in a vector database, and retrieve relevant context at query time.
- **Microsoft.Extensions.VectorData** provides the storage and retrieval layer with `InMemoryVectorStore` for prototyping and pluggable backends for production.
- **Chunking strategies** matter more than most teams expect. Splitting on headers preserves document structure; splitting on token count gives uniform context windows. The right choice depends on your content.

### Tool Calling

- **Tool calling** lets the model invoke your .NET methods with structured arguments. The model reasons about which tool to use, generates the arguments, and your code executes the action.
- Tools are registered via `AIFunctionFactory` and work across any `IChatClient` provider. The model sees tool descriptions and schemas; your code sees strongly-typed method calls.
- The key design decision is granularity: too few coarse tools and the model can't express nuanced intent; too many fine-grained tools and the model struggles to choose.

### The Decision Framework

- **MEAI alone** — Use when you need a single model call with optional tool use. Chat completions, embeddings, structured output. No agent lifecycle needed.
- **Microsoft Agent Framework** — Use when you need agent identity, conversation state, multi-turn tool orchestration, or multi-agent collaboration. The framework manages what you'd otherwise build by hand.
- **MCP** — Use when you need to expose or consume tools across application boundaries. MCP is about interoperability, not orchestration.

## Segment 3: Agent Frameworks and Value

The Microsoft Agent Framework builds on top of MEAI to provide what you'd otherwise have to wire up yourself: agent identity, tool orchestration, conversation state, streaming, and multi-agent coordination. Understanding where frameworks add leverage — and where they add friction — is essential for making good architectural decisions.

The core building block is `ChatClientAgent`, which wraps an `IChatClient` with agent-specific capabilities. You define the agent's system instructions, register its tools, and the framework handles the tool-call loop: the model reasons, selects a tool, your code executes it, and the result feeds back into the conversation. This loop can run for multiple iterations, letting agents complete complex multi-step tasks.

### What the Framework Delivers

- **ChatClientAgent** — An agent built on `IChatClient` that adds system prompts, tool registration, structured output, and multi-turn conversation management. It's the workhorse for most agent scenarios.
- **Tool orchestration** — The framework manages the invoke-observe-reason loop automatically. Register tools, and the agent handles selection, invocation, error recovery, and result integration.
- **State management** — Conversation history, tool results, and agent context are managed by the framework. You focus on business logic, not plumbing.
- **Streaming** — Real-time token streaming for responsive user experiences. The framework handles partial responses and tool-call interleaving.

### Multi-Agent Orchestration

- **Sequential workflows** — Agents execute in order, each building on the output of the previous agent. Useful for pipelines like "research → draft → review."
- **Handoff patterns** — One agent transfers control to another based on the conversation context. A triage agent routes to specialized agents.
- **Group chat** — Multiple agents collaborate in a moderated conversation, contributing their specialized knowledge to solve a problem collectively.

### Where Frameworks Add Friction

Frameworks are not free. They introduce abstraction layers that can obscure what's happening at the model level. Debugging becomes harder when you can't see the raw prompts and completions. For simple scenarios — a single model call with one tool — the framework overhead isn't justified. The rule of thumb: if you're managing conversation state or coordinating multiple agents, the framework pays for itself. If you're making a single call, MEAI is enough.

## Segment 4: Production Readiness

The gap between a working demo and a production deployment is where most AI projects stall. The model works in development, the agent handles happy paths, and then reality hits: inconsistent responses, runaway costs, security concerns, and no way to debug what went wrong. This segment addresses the blocking factors head-on.

Observability is the foundation. Without visibility into what your agents are doing — which tools they're calling, how many tokens they're consuming, what prompts they're sending — you're flying blind. MEAI's `ChatClientBuilder` supports OpenTelemetry middleware out of the box, which means you can trace every model interaction through your existing monitoring infrastructure.

### OpenTelemetry Observability

- **Distributed tracing** — Trace agent interactions end-to-end, from the initial user request through tool calls and back. Each model invocation becomes a span in your trace.
- **Token metrics** — Track prompt and completion token counts per request. Essential for cost monitoring and capacity planning.
- **Custom spans** — Wrap tool executions, RAG retrievals, and agent handoffs in custom spans for granular visibility.
- **Integration with existing infrastructure** — MEAI's OpenTelemetry support plugs into Aspire dashboards, Application Insights, Jaeger, or any OTel-compatible backend.

### Debugging Agent Behavior

- **Prompt inspection** — Log the actual prompts being sent to the model, including system instructions, conversation history, and tool schemas. When an agent misbehaves, the prompt is usually the culprit.
- **Tool-call tracing** — Track which tools the agent called, with what arguments, and what results were returned. Non-deterministic behavior often comes from unexpected tool-call sequences.
- **Reproducibility** — Seed values, temperature settings, and prompt versioning help make agent behavior more predictable and testable.

### Security, Compliance, and Cost

- **Prompt injection defense** — Validate and sanitize user inputs. Use system prompts to constrain agent behavior. Consider output filtering for sensitive domains.
- **Data governance** — Understand what data flows to the model provider. Use Azure OpenAI for data residency requirements. Keep PII out of prompts when possible.
- **Cost control** — Set token budgets, implement rate limiting via `ChatClientBuilder` middleware, and monitor spend per agent and per workflow. A runaway agent loop can burn through budget fast.

## Segment 5: Ecosystem and Interoperability

The Model Context Protocol (MCP) is changing how AI applications share capabilities. Instead of building custom integrations for every tool and service, MCP provides a standard protocol for tool discovery and invocation. For .NET developers, this opens up both directions: exposing your application's capabilities to any MCP-compatible client, and consuming tools from any MCP server.

.NET has first-class MCP support through the official C# SDK. You can build MCP servers that expose your business logic as tools — any MCP client, whether it's GitHub Copilot, Claude, VS Code, or a custom application, can discover and invoke them. You can also build MCP clients that connect to external servers, pulling in capabilities from across your organization or the broader ecosystem.

### .NET as MCP Server and Client

- **MCP Server** — Expose .NET methods as MCP tools with schemas, descriptions, and structured input/output. Your existing business logic becomes available to any AI application that speaks MCP.
- **MCP Client** — Connect to external MCP servers to consume tools and resources. Enrich your agents with capabilities from other teams, services, or third-party providers.
- **Transport options** — Support for stdio (local tools) and HTTP with Server-Sent Events (remote services). Choose based on your deployment model.

### Cross-Framework Interoperability

- **AG-UI protocol** — An emerging standard for agent-to-UI communication, enabling real-time streaming of agent state, tool calls, and results to frontend applications.
- **Framework bridging** — MCP and similar protocols let agents built with different frameworks (MEAI, Semantic Kernel, LangChain, AutoGen) share tools and collaborate without tight coupling.
- **.NET's strategic position** — The combination of MEAI primitives, the Agent Framework, and MCP support positions .NET as both a first-class agent development framework and a primitives provider that other ecosystems can build on.

### Why Interoperability Matters

The AI ecosystem is moving too fast for any single framework to own everything. The winning strategy is building on open protocols and composable primitives. MCP for tool sharing, OpenTelemetry for observability, and standard interfaces like `IChatClient` for provider flexibility. .NET developers who invest in these patterns will find their work composes well with the rest of the ecosystem rather than getting locked into a single stack.

## Segment 6: Priorities and Next Steps

With so many capabilities available, the natural question is: where should the .NET AI ecosystem focus next? This segment force-ranks the priorities that would most accelerate developer adoption and production readiness.

The biggest leverage point is mental model simplification. The ecosystem has powerful tools, but the relationship between MEAI, Semantic Kernel, the Agent Framework, and MCP isn't always obvious to developers encountering them for the first time. Clear guidance on "start here, add this when you need it" would reduce the barrier to entry more than any new feature.

### Priority Rankings

- **Guidance and mental models** — Clear, opinionated documentation on which tool to use for which scenario. Decision trees, not feature matrices. Developers need to know where to start, not everything that's possible.
- **Templates and starter kits** — Production-ready project templates that include observability, error handling, and security patterns from day one. The gap between "hello world" and "production ready" should be a template, not a journey.
- **Tooling and debugging** — Better visibility into agent behavior during development. Prompt inspectors, tool-call visualizers, and agent interaction debuggers would dramatically reduce the time from "it's not working" to "I know why."
- **Ecosystem coherence** — Consistent patterns across MEAI, the Agent Framework, and MCP. When the primitives compose cleanly, developers build with confidence.

### What Would Most Accelerate Adoption

The .NET AI ecosystem doesn't lack capability — it lacks on-ramps. Developers who get past the initial learning curve are productive and enthusiastic. The challenge is making that curve less steep: better docs, more examples, clearer decision frameworks, and templates that encode best practices. Every hour saved on "which library do I use?" is an hour spent building something valuable.

### Closing Thoughts

The .NET AI stack is mature enough for production and moving fast enough to stay current. The foundation — MEAI's provider-agnostic abstractions — is solid. The frameworks — Agent Framework, MCP SDK — are capable. The opportunity is in the developer experience layer: making it obvious, fast, and safe to go from idea to production. Start with MEAI, add frameworks when your scenario demands them, invest in observability from day one, and build on open protocols.

## Resources and Next Steps

- The .NET Developer's Guide to AI Agents: https://github.com/JeremyLikness/dotnet-developer-guide-ai-agents
- Microsoft.Extensions.AI: https://github.com/dotnet/extensions
- Microsoft Agent Framework: https://github.com/microsoft/agent-framework
- Model Context Protocol for .NET: https://github.com/modelcontextprotocol/csharp-sdk
- Microsoft.Extensions.VectorData: https://learn.microsoft.com/dotnet/ai/conceptual/vector-databases
