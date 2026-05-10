namespace Roggemans.GoogleAnalytics.Services;

public sealed record GoogleAnalyticsTrackingRequest(
    string? ClientId,
    string? UserId,
    IReadOnlyList<GoogleAnalyticsTrackingEvent> Events,
    IReadOnlyDictionary<string, object?>? UserProperties = null,
    bool DebugMode = false,
    bool? NonPersonalizedAds = null);
