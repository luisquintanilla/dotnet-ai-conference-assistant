var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddProject<Projects.ConferenceAssistant_Web>("web");

builder.Build().Run();
