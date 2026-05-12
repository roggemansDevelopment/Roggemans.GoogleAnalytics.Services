namespace Roggemans.GoogleAnalytics.ApiClient;

public sealed record GoogleAnalyticsApiResult<T>(
    bool Success,
    T? Data,
    string? ErrorCode,
    string? ErrorMessage,
    int? StatusCode)
{
    public static GoogleAnalyticsApiResult<T> Ok(T data)
    {
        return new(true, data, null, null, null);
    }

    public static GoogleAnalyticsApiResult<T> Fail(
        string? errorCode,
        string? errorMessage,
        int? statusCode)
    {
        return new(false, default, errorCode, errorMessage, statusCode);
    }
}
