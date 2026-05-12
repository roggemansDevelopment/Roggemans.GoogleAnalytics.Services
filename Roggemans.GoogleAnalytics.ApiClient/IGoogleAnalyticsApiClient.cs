using Roggemans.GoogleAnalytics.Services;

namespace Roggemans.GoogleAnalytics.ApiClient;

public interface IGoogleAnalyticsApiClient
{
    Task<GoogleAnalyticsConfigurationStatus> GetConfigurationStatusAsync(
        CancellationToken cancellationToken = default);

    Task<GoogleAnalyticsApiResult<GoogleAnalyticsSummary>> GetDivintageSummaryAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default);

    Task<GoogleAnalyticsApiResult<MeasurementProtocolValidationResult>> ValidateMeasurementProtocolAsync(
        CancellationToken cancellationToken = default);

    Task<GoogleAnalyticsApiResult<GoogleAnalyticsTrackingResult>> TrackAsync(
        GoogleAnalyticsTrackingRequest request,
        CancellationToken cancellationToken = default);
}
