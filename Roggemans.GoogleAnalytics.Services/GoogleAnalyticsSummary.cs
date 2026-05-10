namespace Roggemans.GoogleAnalytics.Services;

public sealed record GoogleAnalyticsSummary(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<string> MetricNames,
    IReadOnlyList<GoogleAnalyticsMetricValue> Totals,
    IReadOnlyList<GoogleAnalyticsDailyMetricRow> Rows,
    DateTimeOffset RetrievedAtUtc);
