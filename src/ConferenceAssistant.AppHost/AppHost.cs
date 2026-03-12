var builder = DistributedApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Azure OpenAI — reference an existing resource via user secrets
// Set with: dotnet user-secrets set "AzureOpenAI:Name" "<resource-name>"
//           dotnet user-secrets set "AzureOpenAI:ResourceGroup" "<resource-group>"
// ---------------------------------------------------------------------------
var azOpenAiName = builder.AddParameterFromConfiguration("AzureOpenAIName", "AzureOpenAI:Name");
var azOpenAiRg = builder.AddParameterFromConfiguration("AzureOpenAIResourceGroup", "AzureOpenAI:ResourceGroup");

var openai = builder.AddAzureOpenAI("openai")
    .RunAsExisting(azOpenAiName, azOpenAiRg);

// Deployment names must match what exists in your Azure OpenAI resource
openai.AddDeployment("chat", "gpt-4o", "2024-08-06");
openai.AddDeployment("embedding", "text-embedding-3-small", "1");

var web = builder.AddProject<Projects.ConferenceAssistant_Web>("web")
    .WithReference(openai)
    .WaitFor(openai);

builder.Build().Run();
