using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Roggemans.GoogleAnalytics.Services;

public sealed class GoogleAnalyticsReportService : IGoogleAnalyticsReportService
{
    private static readonly string[] AnalyticsReadonlyScopes =
    [
        "https://www.googleapis.com/auth/analytics.readonly"
    ];

    private static readonly string[] SummaryMetricNames =
    [
        "activeUsers",
        "sessions",
        "screenPageViews",
        "eventCount"
    ];

    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleAnalyticsReportService> _logger;
    private readonly GoogleAnalyticsOptions _options;

    public GoogleAnalyticsReportService(
        HttpClient httpClient,
        IOptions<GoogleAnalyticsOptions> options,
        ILogger<GoogleAnalyticsReportService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public GoogleAnalyticsConfigurationStatus GetConfigurationStatus()
    {
        return _options.GetConfigurationStatus();
    }

    public async Task<GoogleAnalyticsOperationResult<GoogleAnalyticsSummary>> GetDivintageSummaryAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        GoogleAnalyticsConfigurationStatus status = GetConfigurationStatus();
        if (!status.CanRunReports)
        {
            return GoogleAnalyticsOperationResult<GoogleAnalyticsSummary>.Fail(
                "google_analytics_reporting_not_configured",
                status.ReportingConfigurationMessage);
        }

        DateOnly effectiveEndDate = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly effectiveStartDate = startDate ?? effectiveEndDate.AddDays(-Math.Max(_options.DefaultDateRangeDays, 1));

        if (effectiveStartDate > effectiveEndDate)
        {
            return GoogleAnalyticsOperationResult<GoogleAnalyticsSummary>.Fail(
                "invalid_date_range",
                "Start date must be before or equal to end date.",
                HttpStatusCode.BadRequest);
        }

        try
        {
            using HttpRequestMessage request = await CreateRunReportRequestAsync(
                    effectiveStartDate,
                    effectiveEndDate,
                    cancellationToken)
                .ConfigureAwait(false);

            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            string responseContent = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Google Analytics Data API returned {StatusCode}: {ResponseContent}",
                    response.StatusCode,
                    responseContent);

                return GoogleAnalyticsOperationResult<GoogleAnalyticsSummary>.Fail(
                    "google_analytics_reporting_failed",
                    BuildUpstreamErrorMessage(responseContent),
                    response.StatusCode);
            }

            GoogleAnalyticsSummary summary = ParseRunReportResponse(
                responseContent,
                effectiveStartDate,
                effectiveEndDate);

            return GoogleAnalyticsOperationResult<GoogleAnalyticsSummary>.Ok(summary);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Google Analytics Data API report failed.");

            return GoogleAnalyticsOperationResult<GoogleAnalyticsSummary>.Fail(
                "google_analytics_reporting_exception",
                exception.Message);
        }
    }

    public async Task<GoogleAnalyticsOperationResult<MeasurementProtocolValidationResult>> ValidateMeasurementProtocolAsync(
        CancellationToken cancellationToken = default)
    {
        GoogleAnalyticsConfigurationStatus status = GetConfigurationStatus();
        if (!status.CanValidateMeasurementProtocol)
        {
            return GoogleAnalyticsOperationResult<MeasurementProtocolValidationResult>.Fail(
                "measurement_protocol_not_configured",
                status.MeasurementProtocolConfigurationMessage);
        }

        string measurementId = Uri.EscapeDataString(_options.MeasurementId!.Trim());
        string apiSecret = Uri.EscapeDataString(_options.MeasurementProtocolApiSecret!.Trim());
        Uri requestUri = new($"{_options.MeasurementProtocolDebugBaseUri}?measurement_id={measurementId}&api_secret={apiSecret}");

        object payload = new
        {
            client_id = $"codex-poc.{Guid.NewGuid():N}",
            events = new object[]
            {
                new
                {
                    name = "codex_poc_validation",
                    @params = new
                    {
                        engagement_time_msec = 1,
                        session_id = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
                    }
                }
            }
        };

        try
        {
            using HttpResponseMessage response = await _httpClient
                .PostAsJsonAsync(requestUri, payload, cancellationToken)
                .ConfigureAwait(false);

            string responseContent = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Google Analytics Measurement Protocol debug endpoint returned {StatusCode}: {ResponseContent}",
                    response.StatusCode,
                    responseContent);

                return GoogleAnalyticsOperationResult<MeasurementProtocolValidationResult>.Fail(
                    "measurement_protocol_validation_failed",
                    BuildUpstreamErrorMessage(responseContent),
                    response.StatusCode);
            }

            MeasurementProtocolValidationResult validationResult = ParseMeasurementProtocolValidationResponse(responseContent);
            return GoogleAnalyticsOperationResult<MeasurementProtocolValidationResult>.Ok(validationResult);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Google Analytics Measurement Protocol debug validation failed.");

            return GoogleAnalyticsOperationResult<MeasurementProtocolValidationResult>.Fail(
                "measurement_protocol_validation_exception",
                exception.Message);
        }
    }

    private async Task<HttpRequestMessage> CreateRunReportRequestAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        string propertyName = _options.GetNormalizedPropertyName();
        string accessToken = !string.IsNullOrWhiteSpace(_options.AccessToken)
            ? _options.AccessToken.Trim()
            : await CreateServiceAccountAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        Uri requestUri = new(_options.DataApiBaseUri, $"v1beta/{propertyName}:runReport");
        object payload = new
        {
            dateRanges = new object[]
            {
                new
                {
                    startDate = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    endDate = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                }
            },
            dimensions = new object[]
            {
                new { name = "date" }
            },
            metrics = SummaryMetricNames.Select(metricName => new { name = metricName }).ToArray(),
            orderBys = new object[]
            {
                new
                {
                    dimension = new
                    {
                        dimensionName = "date"
                    }
                }
            }
        };

        HttpRequestMessage request = new(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
    }

    private async Task<string> CreateServiceAccountAccessTokenAsync(CancellationToken cancellationToken)
    {
        string? serviceAccountJson = _options.TryGetServiceAccountJson();
        if (string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            throw new InvalidOperationException(
                "Google Analytics reporting requires service account JSON, service account JSON base64, a credentials path, or an access token.");
        }

        using MemoryStream serviceAccountStream = new(Encoding.UTF8.GetBytes(serviceAccountJson));
        GoogleCredential credential = ServiceAccountCredential
            .FromServiceAccountData(serviceAccountStream)
            .ToGoogleCredential()
            .CreateScoped(AnalyticsReadonlyScopes);

        return await credential.UnderlyingCredential
            .GetAccessTokenForRequestAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static GoogleAnalyticsSummary ParseRunReportResponse(
        string responseContent,
        DateOnly startDate,
        DateOnly endDate)
    {
        using JsonDocument document = JsonDocument.Parse(responseContent);
        JsonElement root = document.RootElement;

        IReadOnlyList<string> metricNames = root.TryGetProperty("metricHeaders", out JsonElement metricHeaders)
            ? metricHeaders.EnumerateArray()
                .Select(header => header.GetProperty("name").GetString() ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray()
            : SummaryMetricNames;

        List<GoogleAnalyticsDailyMetricRow> rows = [];
        if (root.TryGetProperty("rows", out JsonElement responseRows))
        {
            foreach (JsonElement row in responseRows.EnumerateArray())
            {
                DateOnly date = ParseGoogleAnalyticsDate(row);
                IReadOnlyList<GoogleAnalyticsMetricValue> metrics = ParseMetricValues(row, metricNames);
                rows.Add(new GoogleAnalyticsDailyMetricRow(date, metrics));
            }
        }

        IReadOnlyList<GoogleAnalyticsMetricValue> totals = CalculateTotals(rows, metricNames);

        return new GoogleAnalyticsSummary(
            startDate,
            endDate,
            metricNames,
            totals,
            rows,
            DateTimeOffset.UtcNow);
    }

    private static DateOnly ParseGoogleAnalyticsDate(JsonElement row)
    {
        if (!row.TryGetProperty("dimensionValues", out JsonElement dimensionValues))
        {
            return DateOnly.MinValue;
        }

        JsonElement firstDimension = dimensionValues
            .EnumerateArray()
            .FirstOrDefault();

        if (firstDimension.ValueKind == JsonValueKind.Undefined
            || !firstDimension.TryGetProperty("value", out JsonElement dateElement))
        {
            return DateOnly.MinValue;
        }

        string? dateValue = dateElement.GetString();

        return DateOnly.TryParseExact(
            dateValue,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly parsedDate)
            ? parsedDate
            : DateOnly.MinValue;
    }

    private static IReadOnlyList<GoogleAnalyticsMetricValue> ParseMetricValues(
        JsonElement row,
        IReadOnlyList<string> metricNames)
    {
        if (!row.TryGetProperty("metricValues", out JsonElement metricValues))
        {
            return [];
        }

        return metricValues
            .EnumerateArray()
            .Select((metricValue, index) =>
            {
                string rawValue = metricValue.GetProperty("value").GetString() ?? string.Empty;
                string metricName = index < metricNames.Count ? metricNames[index] : $"metric_{index}";
                decimal? decimalValue = decimal.TryParse(
                    rawValue,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out decimal parsed)
                    ? parsed
                    : null;

                return new GoogleAnalyticsMetricValue(metricName, rawValue, decimalValue);
            })
            .ToArray();
    }

    private static IReadOnlyList<GoogleAnalyticsMetricValue> CalculateTotals(
        IReadOnlyList<GoogleAnalyticsDailyMetricRow> rows,
        IReadOnlyList<string> metricNames)
    {
        return metricNames
            .Select(metricName =>
            {
                decimal total = rows
                    .SelectMany(row => row.Metrics)
                    .Where(metric => string.Equals(metric.Name, metricName, StringComparison.Ordinal))
                    .Where(metric => metric.DecimalValue.HasValue)
                    .Sum(metric => metric.DecimalValue!.Value);

                return new GoogleAnalyticsMetricValue(
                    metricName,
                    total.ToString(CultureInfo.InvariantCulture),
                    total);
            })
            .ToArray();
    }

    private static MeasurementProtocolValidationResult ParseMeasurementProtocolValidationResponse(string responseContent)
    {
        using JsonDocument document = JsonDocument.Parse(responseContent);
        JsonElement root = document.RootElement;

        IReadOnlyList<MeasurementProtocolValidationMessage> messages =
            root.TryGetProperty("validationMessages", out JsonElement validationMessages)
                ? validationMessages.EnumerateArray()
                    .Select(message => new MeasurementProtocolValidationMessage(
                        TryGetString(message, "fieldPath"),
                        TryGetString(message, "description"),
                        TryGetString(message, "validationCode")))
                    .ToArray()
                : [];

        return new MeasurementProtocolValidationResult(
            messages.Count == 0,
            messages,
            DateTimeOffset.UtcNow);
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property)
            ? property.GetString()
            : null;
    }

    private static string BuildUpstreamErrorMessage(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return "Google Analytics returned an empty error response.";
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(responseContent);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("error", out JsonElement error)
                && error.TryGetProperty("message", out JsonElement message))
            {
                return message.GetString() ?? responseContent;
            }
        }
        catch (JsonException)
        {
            return responseContent;
        }

        return responseContent;
    }
}
