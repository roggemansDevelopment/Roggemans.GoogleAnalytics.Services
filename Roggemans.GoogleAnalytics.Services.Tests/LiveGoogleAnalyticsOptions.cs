using Roggemans.GoogleAnalytics.Services;

namespace Roggemans.GoogleAnalytics.Services.Tests;

internal static class LiveGoogleAnalyticsOptions
{
    public static GoogleAnalyticsOptions FromEnvironment()
    {
        return new GoogleAnalyticsOptions
        {
            PropertyId = Read("GoogleAnalytics__PropertyId", "GOOGLE_ANALYTICS_PROPERTY_ID_DIVINTAGE"),
            MeasurementId = Read("GoogleAnalytics__MeasurementId", "GOOGLE_ANALYTICS_MEASUREMENT_ID_DIVINTAGE") ?? "G-XJWB055WRG",
            MeasurementProtocolApiSecret = Read(
                "GoogleAnalytics__MeasurementProtocolApiSecret",
                "GOOGLE_ANALYTICS_MEASUREMENT_PROTOCOL_API_SECRET_DIVINTAGE"),
            ServiceAccountJson = Read("GoogleAnalytics__ServiceAccountJson", "GOOGLE_ANALYTICS_SERVICE_ACCOUNT_JSON_DIVINTAGE"),
            ServiceAccountJsonBase64 = Read(
                "GoogleAnalytics__ServiceAccountJsonBase64",
                "GOOGLE_ANALYTICS_SERVICE_ACCOUNT_JSON_BASE64_DIVINTAGE"),
            ServiceAccountCredentialsPath = Read(
                "GoogleAnalytics__ServiceAccountCredentialsPath",
                "GOOGLE_ANALYTICS_SERVICE_ACCOUNT_CREDENTIALS_PATH_DIVINTAGE"),
            OAuthClientId = Read("GoogleAnalytics__OAuthClientId", "GOOGLE_ANALYTICS_OAUTH_CLIENT_ID_DIVINTAGE"),
            OAuthClientSecret = Read("GoogleAnalytics__OAuthClientSecret", "GOOGLE_ANALYTICS_OAUTH_CLIENT_SECRET_DIVINTAGE"),
            OAuthRefreshToken = Read("GoogleAnalytics__OAuthRefreshToken", "GOOGLE_ANALYTICS_OAUTH_REFRESH_TOKEN_DIVINTAGE"),
            AccessToken = Read("GoogleAnalytics__AccessToken", "GOOGLE_ANALYTICS_ACCESS_TOKEN_DIVINTAGE")
        };
    }

    public static bool RequireLiveReports()
    {
        string? value = Read("GoogleAnalytics__RequireLiveReports", "GOOGLE_ANALYTICS_REQUIRE_LIVE_REPORTS_DIVINTAGE");
        return bool.TryParse(value, out bool requireLiveReports) && requireLiveReports;
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
