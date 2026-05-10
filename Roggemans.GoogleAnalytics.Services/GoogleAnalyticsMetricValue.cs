namespace Roggemans.GoogleAnalytics.Services;

public sealed record GoogleAnalyticsMetricValue(
    string Name,
    string RawValue,
    decimal? DecimalValue);
