namespace Roggemans.GoogleAnalytics.Services;

public sealed record GoogleAnalyticsTrackingEvent(
    string Name,
    IReadOnlyDictionary<string, object?> Parameters);
