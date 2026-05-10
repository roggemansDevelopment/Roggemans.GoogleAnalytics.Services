namespace Roggemans.GoogleAnalytics.Services;

public sealed record GoogleAnalyticsTrackingResult(
    bool IsValid,
    IReadOnlyList<string> EventNames,
    IReadOnlyList<MeasurementProtocolValidationMessage> ValidationMessages,
    bool DebugMode,
    DateTimeOffset RetrievedAtUtc);
