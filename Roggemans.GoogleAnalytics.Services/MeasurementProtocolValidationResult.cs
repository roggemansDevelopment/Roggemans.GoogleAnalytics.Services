namespace Roggemans.GoogleAnalytics.Services;

public sealed record MeasurementProtocolValidationResult(
    bool IsValid,
    IReadOnlyList<MeasurementProtocolValidationMessage> ValidationMessages,
    DateTimeOffset RetrievedAtUtc);
