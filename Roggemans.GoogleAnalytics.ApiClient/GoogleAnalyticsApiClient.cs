using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Roggemans.GoogleAnalytics.Services;

namespace Roggemans.GoogleAnalytics.ApiClient;

public sealed class GoogleAnalyticsApiClient(HttpClient httpClient) : IGoogleAnalyticsApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GoogleAnalyticsConfigurationStatus> GetConfigurationStatusAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient
            .GetAsync("config", cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        GoogleAnalyticsConfigurationStatus? status = await response.Content
            .ReadFromJsonAsync<GoogleAnalyticsConfigurationStatus>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return status ?? throw new InvalidOperationException("Google Analytics API returned an empty configuration status.");
    }

    public async Task<GoogleAnalyticsApiResult<GoogleAnalyticsSummary>> GetDivintageSummaryAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        string path = BuildSummaryPath(startDate, endDate);
        using HttpResponseMessage response = await httpClient
            .GetAsync(path, cancellationToken)
            .ConfigureAwait(false);

        return await ReadApiResultAsync<GoogleAnalyticsSummary>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GoogleAnalyticsApiResult<MeasurementProtocolValidationResult>> ValidateMeasurementProtocolAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient
            .PostAsync("api/google-analytics/measurement-protocol/validate", null, cancellationToken)
            .ConfigureAwait(false);

        return await ReadApiResultAsync<MeasurementProtocolValidationResult>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GoogleAnalyticsApiResult<GoogleAnalyticsTrackingResult>> TrackAsync(
        GoogleAnalyticsTrackingRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient
            .PostAsJsonAsync("api/google-analytics/measurement-protocol/track", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return await ReadApiResultAsync<GoogleAnalyticsTrackingResult>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildSummaryPath(DateOnly? startDate, DateOnly? endDate)
    {
        List<string> query = [];

        if (startDate.HasValue)
        {
            query.Add($"startDate={Uri.EscapeDataString(startDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}");
        }

        if (endDate.HasValue)
        {
            query.Add($"endDate={Uri.EscapeDataString(endDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}");
        }

        string path = "api/google-analytics/divintage/summary";
        return query.Count == 0 ? path : $"{path}?{string.Join('&', query)}";
    }

    private static async Task<GoogleAnalyticsApiResult<T>> ReadApiResultAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            T? payload = await response.Content
                .ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (payload is null)
            {
                return GoogleAnalyticsApiResult<T>.Fail(
                    "empty_response",
                    "Google Analytics API returned an empty response.",
                    (int)response.StatusCode);
            }

            return GoogleAnalyticsApiResult<T>.Ok(payload);
        }

        ApiProblemDetails problem = await ReadProblemDetailsAsync(response, cancellationToken).ConfigureAwait(false);
        return GoogleAnalyticsApiResult<T>.Fail(
            problem.Title ?? response.ReasonPhrase,
            problem.Detail ?? response.ReasonPhrase,
            problem.Status ?? (int)response.StatusCode);
    }

    private static async Task<ApiProblemDetails> ReadProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ApiProblemDetails(null, response.ReasonPhrase, (int)response.StatusCode);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;

            string? title = root.TryGetProperty("title", out JsonElement titleElement)
                && titleElement.ValueKind == JsonValueKind.String
                    ? titleElement.GetString()
                    : null;

            string? detail = root.TryGetProperty("detail", out JsonElement detailElement)
                && detailElement.ValueKind == JsonValueKind.String
                    ? detailElement.GetString()
                    : null;

            int? status = root.TryGetProperty("status", out JsonElement statusElement)
                && statusElement.ValueKind == JsonValueKind.Number
                && statusElement.TryGetInt32(out int statusValue)
                    ? statusValue
                    : null;

            return new ApiProblemDetails(title, detail, status);
        }
        catch (JsonException)
        {
            return new ApiProblemDetails(null, content, (int)response.StatusCode);
        }
    }

    private sealed record ApiProblemDetails(string? Title, string? Detail, int? Status);
}
