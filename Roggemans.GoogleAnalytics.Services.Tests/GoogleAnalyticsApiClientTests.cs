using System.Net;
using System.Text.Json;
using Roggemans.GoogleAnalytics.ApiClient;
using Roggemans.GoogleAnalytics.Services;

namespace Roggemans.GoogleAnalytics.Services.Tests;

public sealed class GoogleAnalyticsApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetDivintageSummaryAsync_UsesApiSummaryEndpointWithDateQuery()
    {
        using StubHttpMessageHandler handler = new(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                "/api/google-analytics/divintage/summary?startDate=2026-05-01&endDate=2026-05-12",
                request.RequestUri?.PathAndQuery);

            GoogleAnalyticsSummary summary = new(
                new DateOnly(2026, 5, 1),
                new DateOnly(2026, 5, 12),
                ["activeUsers"],
                [new GoogleAnalyticsMetricValue("activeUsers", "12", 12)],
                [],
                DateTimeOffset.Parse("2026-05-12T09:00:00Z"));

            return JsonResponse(summary);
        });

        GoogleAnalyticsApiClient client = CreateClient(handler);

        GoogleAnalyticsApiResult<GoogleAnalyticsSummary> result =
            await client.GetDivintageSummaryAsync(
                new DateOnly(2026, 5, 1),
                new DateOnly(2026, 5, 12));

        Assert.True(result.Success);
        Assert.Equal(new DateOnly(2026, 5, 1), result.Data?.StartDate);
        Assert.Equal(new DateOnly(2026, 5, 12), result.Data?.EndDate);
    }

    [Fact]
    public async Task TrackAsync_PostsTrackingRequestToApi()
    {
        using StubHttpMessageHandler handler = new(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/google-analytics/measurement-protocol/track", request.RequestUri?.PathAndQuery);

            string requestBody = await request.Content!.ReadAsStringAsync();
            Assert.Contains("page_view", requestBody, StringComparison.Ordinal);
            Assert.Contains("test-client", requestBody, StringComparison.Ordinal);

            GoogleAnalyticsTrackingResult trackingResult = new(
                true,
                ["page_view"],
                [],
                false,
                DateTimeOffset.Parse("2026-05-12T09:00:00Z"));

            return JsonResponse(trackingResult);
        });

        GoogleAnalyticsApiClient client = CreateClient(handler);
        GoogleAnalyticsTrackingRequest trackingRequest = new(
            "test-client",
            null,
            [new GoogleAnalyticsTrackingEvent("page_view", new Dictionary<string, object?>())]);

        GoogleAnalyticsApiResult<GoogleAnalyticsTrackingResult> result =
            await client.TrackAsync(trackingRequest);

        Assert.True(result.Success);
        Assert.Equal(["page_view"], result.Data?.EventNames);
    }

    [Fact]
    public async Task GetDivintageSummaryAsync_ReturnsProblemDetailsOnApiFailure()
    {
        using StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = JsonContent(new
            {
                title = "google_analytics_reporting_not_configured",
                detail = "Reporting credentials are missing.",
                status = 503
            })
        });

        GoogleAnalyticsApiClient client = CreateClient(handler);

        GoogleAnalyticsApiResult<GoogleAnalyticsSummary> result =
            await client.GetDivintageSummaryAsync();

        Assert.False(result.Success);
        Assert.Equal("google_analytics_reporting_not_configured", result.ErrorCode);
        Assert.Equal("Reporting credentials are missing.", result.ErrorMessage);
        Assert.Equal(503, result.StatusCode);
    }

    private static GoogleAnalyticsApiClient CreateClient(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://google-analytics-api.test/")
        };

        return new GoogleAnalyticsApiClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(payload)
        };
    }

    private static StringContent JsonContent<T>(T payload)
    {
        return new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            : this(request => Task.FromResult(handler(request)))
        {
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
