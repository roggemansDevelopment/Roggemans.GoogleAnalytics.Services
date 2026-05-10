using System.Net;

namespace Roggemans.GoogleAnalytics.Services;

public sealed record GoogleAnalyticsOperationResult<T>(
    bool Success,
    T? Data,
    string? ErrorCode,
    string? ErrorMessage,
    HttpStatusCode? StatusCode = null)
{
    public static GoogleAnalyticsOperationResult<T> Ok(T data)
    {
        return new(true, data, null, null);
    }

    public static GoogleAnalyticsOperationResult<T> Fail(
        string errorCode,
        string errorMessage,
        HttpStatusCode? statusCode = null)
    {
        return new(false, default, errorCode, errorMessage, statusCode);
    }
}
