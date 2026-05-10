using Roggemans.GoogleAnalytics.Services;

namespace Roggemans.GoogleAnalytics.Services.Tests;

public sealed class GoogleAnalyticsOptionsTests
{
    [Fact]
    public void GetConfigurationStatus_reports_missing_reporting_credentials()
    {
        GoogleAnalyticsOptions options = new()
        {
            MeasurementId = "G-TEST123"
        };

        GoogleAnalyticsConfigurationStatus status = options.GetConfigurationStatus();

        Assert.False(status.CanRunReports);
        Assert.False(status.HasPropertyId);
        Assert.False(status.HasReportingCredential);
        Assert.Contains("PropertyId", status.ReportingConfigurationMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void GetConfigurationStatus_reports_oauth_refresh_token_credentials_ready()
    {
        GoogleAnalyticsOptions options = new()
        {
            PropertyId = "123456789",
            OAuthClientId = "oauth-client-id",
            OAuthClientSecret = "oauth-client-secret",
            OAuthRefreshToken = "oauth-refresh-token"
        };

        GoogleAnalyticsConfigurationStatus status = options.GetConfigurationStatus();

        Assert.True(status.CanRunReports);
        Assert.True(status.HasOAuthRefreshToken);
    }

    [Fact]
    public void GetConfigurationStatus_reports_measurement_protocol_validation_ready()
    {
        GoogleAnalyticsOptions options = new()
        {
            MeasurementId = "G-TEST123",
            MeasurementProtocolApiSecret = "measurement-protocol-secret"
        };

        GoogleAnalyticsConfigurationStatus status = options.GetConfigurationStatus();

        Assert.True(status.CanValidateMeasurementProtocol);
        Assert.True(status.HasMeasurementId);
        Assert.True(status.HasMeasurementProtocolApiSecret);
    }

    [Fact]
    public void GetNormalizedPropertyName_accepts_prefixed_and_unprefixed_property_ids()
    {
        GoogleAnalyticsOptions prefixed = new()
        {
            PropertyId = "properties/123456789"
        };
        GoogleAnalyticsOptions unprefixed = new()
        {
            PropertyId = "123456789"
        };

        Assert.Equal("properties/123456789", prefixed.GetNormalizedPropertyName());
        Assert.Equal("properties/123456789", unprefixed.GetNormalizedPropertyName());
    }
}
