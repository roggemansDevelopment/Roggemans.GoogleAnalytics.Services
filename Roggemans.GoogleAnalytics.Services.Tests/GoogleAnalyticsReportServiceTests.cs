using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Roggemans.GoogleAnalytics.Services;
using Xunit.Abstractions;

namespace Roggemans.GoogleAnalytics.Services.Tests;

public sealed class GoogleAnalyticsReportServiceTests
{
    private readonly ITestOutputHelper _output;

    public GoogleAnalyticsReportServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetDivintageSummaryAsync_returns_configuration_error_when_reporting_credentials_are_missing()
    {
        GoogleAnalyticsReportService service = CreateService(new GoogleAnalyticsOptions());

        GoogleAnalyticsOperationResult<GoogleAnalyticsSummary> result =
            await service.GetDivintageSummaryAsync();

        Assert.False(result.Success);
        Assert.Equal("google_analytics_reporting_not_configured", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateMeasurementProtocolAsync_posts_to_debug_endpoint_without_collecting_event()
    {
        bool requestWasSent = false;
        HttpMessageHandler handler = new StubHttpMessageHandler(request =>
        {
            requestWasSent = true;

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("debug/mp/collect", request.RequestUri?.AbsoluteUri, StringComparison.Ordinal);
            Assert.Contains("measurement_id=G-TEST123", request.RequestUri?.Query, StringComparison.Ordinal);
            Assert.Contains("api_secret=", request.RequestUri?.Query, StringComparison.Ordinal);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"validationMessages":[]}""")
            };
        });

        GoogleAnalyticsReportService service = CreateService(
            new GoogleAnalyticsOptions
            {
                MeasurementId = "G-TEST123",
                MeasurementProtocolApiSecret = "measurement-protocol-secret"
            },
            handler);

        GoogleAnalyticsOperationResult<MeasurementProtocolValidationResult> result =
            await service.ValidateMeasurementProtocolAsync();

        Assert.True(requestWasSent);
        Assert.True(result.Success);
        Assert.True(result.Data?.IsValid);
    }

    [Fact]
    public async Task Live_measurement_protocol_probe_succeeds_when_runtime_secret_is_configured()
    {
        GoogleAnalyticsOptions options = LiveGoogleAnalyticsOptions.FromEnvironment();
        if (!options.GetConfigurationStatus().CanValidateMeasurementProtocol)
        {
            _output.WriteLine("Live Measurement Protocol probe skipped because measurement id or API secret is not configured.");
            return;
        }

        GoogleAnalyticsReportService service = CreateService(options);

        GoogleAnalyticsOperationResult<MeasurementProtocolValidationResult> result =
            await service.ValidateMeasurementProtocolAsync();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsValid, string.Join(Environment.NewLine, result.Data.ValidationMessages.Select(message => message.Description)));
    }

    [Fact]
    public async Task Live_reporting_probe_returns_rows_when_property_and_read_credentials_are_configured()
    {
        GoogleAnalyticsOptions options = LiveGoogleAnalyticsOptions.FromEnvironment();
        if (!options.GetConfigurationStatus().CanRunReports)
        {
            _output.WriteLine("Live report probe skipped because property id plus OAuth/service-account reporting credentials are not configured.");
            return;
        }

        GoogleAnalyticsReportService service = CreateService(options);

        GoogleAnalyticsOperationResult<GoogleAnalyticsSummary> result =
            await service.GetDivintageSummaryAsync();

        if (!result.Success)
        {
            string error = $"Live report probe did not retrieve data: {result.ErrorCode} - {result.ErrorMessage}";
            _output.WriteLine(error);

            if (LiveGoogleAnalyticsOptions.RequireLiveReports())
            {
                Assert.Fail(error);
            }

            return;
        }

        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data!.MetricNames);
        Assert.NotNull(result.Data.Rows);
    }

    private static GoogleAnalyticsReportService CreateService(
        GoogleAnalyticsOptions options,
        HttpMessageHandler? handler = null)
    {
        HttpClient httpClient = handler is null ? new HttpClient() : new HttpClient(handler);

        return new GoogleAnalyticsReportService(
            httpClient,
            Options.Create(options),
            NullLogger<GoogleAnalyticsReportService>.Instance);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
