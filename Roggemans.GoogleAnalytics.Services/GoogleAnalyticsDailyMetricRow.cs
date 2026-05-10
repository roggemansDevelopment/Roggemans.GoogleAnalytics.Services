namespace Roggemans.GoogleAnalytics.Services;

public sealed record GoogleAnalyticsDailyMetricRow(
    DateOnly Date,
    IReadOnlyList<GoogleAnalyticsMetricValue> Metrics);
