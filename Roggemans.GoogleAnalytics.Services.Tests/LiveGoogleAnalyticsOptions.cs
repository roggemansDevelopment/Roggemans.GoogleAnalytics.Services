using Roggemans.GoogleAnalytics.Services;

namespace Roggemans.GoogleAnalytics.Services.Tests;

internal static class LiveGoogleAnalyticsOptions
{
    public static GoogleAnalyticsOptions FromEnvironment()
    {
        return new GoogleAnalyticsOptions
        {
            PropertyId = Read("GoogleAnalytics__PropertyId", "GOOGLE_ANALYTICS_PROPERTY_ID"),
            MeasurementId = Read("GoogleAnalytics__MeasurementId", "GOOGLE_ANALYTICS_MEASUREMENT_ID") ?? "G-XJWB055WRG",
            MeasurementProtocolApiSecret = Read(
                "GoogleAnalytics__MeasurementProtocolApiSecret",
                "GOOGLE_ANALYTICS_MEASUREMENT_PROTOCOL_API_SECRET"),
            ServiceAccountJson = Read("GoogleAnalytics__ServiceAccountJson", "GOOGLE_ANALYTICS_SERVICE_ACCOUNT_JSON"),
            ServiceAccountJsonBase64 = Read(
                "GoogleAnalytics__ServiceAccountJsonBase64",
                "GOOGLE_ANALYTICS_SERVICE_ACCOUNT_JSON_BASE64"),
            ServiceAccountCredentialsPath = Read(
                "GoogleAnalytics__ServiceAccountCredentialsPath",
                "GOOGLE_ANALYTICS_SERVICE_ACCOUNT_CREDENTIALS_PATH"),
            AccessToken = Read("GoogleAnalytics__AccessToken", "GOOGLE_ANALYTICS_ACCESS_TOKEN")
        };
    }

    private static string? Read(params string[] names)
    {
        foreach (string name in names)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
