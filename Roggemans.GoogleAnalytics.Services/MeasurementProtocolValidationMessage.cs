namespace Roggemans.GoogleAnalytics.Services;

public sealed record MeasurementProtocolValidationMessage(
    string? FieldPath,
    string? Description,
    string? ValidationCode);
