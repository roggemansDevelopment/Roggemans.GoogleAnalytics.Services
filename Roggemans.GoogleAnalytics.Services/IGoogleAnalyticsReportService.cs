namespace Roggemans.GoogleAnalytics.Services;

public interface IGoogleAnalyticsReportService
{
    GoogleAnalyticsConfigurationStatus GetConfigurationStatus();

    Task<GoogleAnalyticsOperationResult<GoogleAnalyticsSummary>> GetDivintageSummaryAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default);

    Task<GoogleAnalyticsOperationResult<MeasurementProtocolValidationResult>> ValidateMeasurementProtocolAsync(
        CancellationToken cancellationToken = default);
}
