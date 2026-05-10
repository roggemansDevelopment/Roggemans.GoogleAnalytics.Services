namespace Roggemans.GoogleAnalytics.Services;

public sealed record GoogleAnalyticsConfigurationStatus(
    bool HasPropertyId,
    bool HasReportingCredential,
    bool HasAccessToken,
    bool HasServiceAccountJson,
    bool HasServiceAccountJsonBase64,
    bool HasServiceAccountCredentialsPath,
    bool HasMeasurementId,
    bool HasMeasurementProtocolApiSecret,
    int DefaultDateRangeDays,
    bool CanRunReports,
    bool CanValidateMeasurementProtocol,
    string ReportingConfigurationMessage,
    string MeasurementProtocolConfigurationMessage);
