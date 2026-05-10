namespace Roggemans.GoogleAnalytics.Mcp.Features.Mcp;

public sealed class GoogleAnalyticsMcpOptions
{
    public const string SectionName = "Mcp";

    public string? ApiKey { get; set; }

    public string ServerName { get; set; } = "Roggemans GoogleAnalytics MCP";

    public string DefaultClientId { get; set; } = "roggemans-googleanalytics-mcp";
}
