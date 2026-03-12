namespace ConferenceAssistant.Mcp.Clients;

public interface IMcpContentClient
{
    Task<string?> FetchContentAsync(string serverName, string toolName, Dictionary<string, object?>? arguments = null);
    Task<IReadOnlyList<string>> ListToolsAsync(string serverName);
}
