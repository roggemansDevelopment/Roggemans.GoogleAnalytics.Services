using System.Text;

namespace Roggemans.GoogleAnalytics.Services;

public sealed class GoogleAnalyticsOptions
{
    public const string SectionName = "GoogleAnalytics";

    public string? PropertyId { get; set; }

    public string? MeasurementId { get; set; }

    public string? MeasurementProtocolApiSecret { get; set; }

    public string? ServiceAccountJson { get; set; }

    public string? ServiceAccountJsonBase64 { get; set; }

    public string? ServiceAccountCredentialsPath { get; set; }

    public string? AccessToken { get; set; }

    public int DefaultDateRangeDays { get; set; } = 7;

    public Uri DataApiBaseUri { get; set; } = new("https://analyticsdata.googleapis.com/");

    public Uri MeasurementProtocolDebugBaseUri { get; set; } = new("https://www.google-analytics.com/debug/mp/collect");

    public GoogleAnalyticsConfigurationStatus GetConfigurationStatus()
    {
        bool hasPropertyId = !string.IsNullOrWhiteSpace(PropertyId);
        bool hasAccessToken = !string.IsNullOrWhiteSpace(AccessToken);
        bool hasServiceAccountJson = !string.IsNullOrWhiteSpace(ServiceAccountJson);
        bool hasServiceAccountJsonBase64 = !string.IsNullOrWhiteSpace(ServiceAccountJsonBase64);
        bool hasServiceAccountCredentialsPath = !string.IsNullOrWhiteSpace(ServiceAccountCredentialsPath);
        bool hasReportingCredential =
            hasAccessToken
            || hasServiceAccountJson
            || hasServiceAccountJsonBase64
            || hasServiceAccountCredentialsPath;
        bool hasMeasurementId = !string.IsNullOrWhiteSpace(MeasurementId);
        bool hasMeasurementProtocolApiSecret = !string.IsNullOrWhiteSpace(MeasurementProtocolApiSecret);
        bool canRunReports = hasPropertyId && hasReportingCredential;
        bool canValidateMeasurementProtocol = hasMeasurementId && hasMeasurementProtocolApiSecret;

        return new GoogleAnalyticsConfigurationStatus(
            hasPropertyId,
            hasReportingCredential,
            hasAccessToken,
            hasServiceAccountJson,
            hasServiceAccountJsonBase64,
            hasServiceAccountCredentialsPath,
            hasMeasurementId,
            hasMeasurementProtocolApiSecret,
            DefaultDateRangeDays,
            canRunReports,
            canValidateMeasurementProtocol,
            canRunReports
                ? "Google Analytics Data API report credentials are configured."
                : "Google Analytics Data API reporting requires GoogleAnalytics__PropertyId plus GoogleAnalytics__ServiceAccountJsonBase64, GoogleAnalytics__ServiceAccountJson, GoogleAnalytics__ServiceAccountCredentialsPath, or GoogleAnalytics__AccessToken.",
            canValidateMeasurementProtocol
                ? "Google Analytics Measurement Protocol debug validation is configured."
                : "Measurement Protocol debug validation requires GoogleAnalytics__MeasurementId plus GoogleAnalytics__MeasurementProtocolApiSecret.");
    }

    public string GetNormalizedPropertyName()
    {
        if (string.IsNullOrWhiteSpace(PropertyId))
        {
            throw new InvalidOperationException("GoogleAnalytics__PropertyId is required.");
        }

        string trimmed = PropertyId.Trim();
        return trimmed.StartsWith("properties/", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"properties/{trimmed}";
    }

    public string? TryGetServiceAccountJson()
    {
        if (!string.IsNullOrWhiteSpace(ServiceAccountJson))
        {
            return ServiceAccountJson;
        }

        if (!string.IsNullOrWhiteSpace(ServiceAccountJsonBase64))
        {
            byte[] bytes = Convert.FromBase64String(ServiceAccountJsonBase64);
            return Encoding.UTF8.GetString(bytes);
        }

        if (!string.IsNullOrWhiteSpace(ServiceAccountCredentialsPath))
        {
            return File.ReadAllText(ServiceAccountCredentialsPath);
        }

        return null;
    }
}
