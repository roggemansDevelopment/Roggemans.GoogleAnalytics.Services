namespace Roggemans.GoogleAnalytics.ApiClient;

public sealed class GoogleAnalyticsApiClientOptions
{
    public const string SectionName = "GoogleAnalyticsApi";

    public string BaseUrl { get; set; } = "http://localhost:5188/";

    public int TimeoutSeconds { get; set; } = 30;
}
