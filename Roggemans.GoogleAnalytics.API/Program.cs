using System.Net;
using Roggemans.GoogleAnalytics.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGoogleAnalyticsServices(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapGet(
    "/",
    () => Results.Ok(new
    {
        service = "Roggemans.GoogleAnalytics.API",
        status = "Running"
    }));

app.MapGet(
    "/config",
    (IGoogleAnalyticsReportService googleAnalyticsReportService) =>
        Results.Ok(googleAnalyticsReportService.GetConfigurationStatus()));

app.MapGet(
    "/api/google-analytics/divintage/summary",
    async (
        DateOnly? startDate,
        DateOnly? endDate,
        IGoogleAnalyticsReportService googleAnalyticsReportService,
        CancellationToken cancellationToken) =>
    {
        GoogleAnalyticsOperationResult<GoogleAnalyticsSummary> result =
            await googleAnalyticsReportService
                .GetDivintageSummaryAsync(startDate, endDate, cancellationToken)
                .ConfigureAwait(false);

        return ToHttpResult(result);
    });

app.MapPost(
    "/api/google-analytics/measurement-protocol/track",
    async (
        GoogleAnalyticsTrackingRequest trackingRequest,
        IGoogleAnalyticsReportService googleAnalyticsReportService,
        CancellationToken cancellationToken) =>
    {
        GoogleAnalyticsOperationResult<GoogleAnalyticsTrackingResult> result =
            await googleAnalyticsReportService
                .TrackAsync(trackingRequest, cancellationToken)
                .ConfigureAwait(false);

        return ToHttpResult(result);
    });

app.MapPost(
    "/api/google-analytics/measurement-protocol/validate",
    async (
        IGoogleAnalyticsReportService googleAnalyticsReportService,
        CancellationToken cancellationToken) =>
    {
        GoogleAnalyticsOperationResult<MeasurementProtocolValidationResult> result =
            await googleAnalyticsReportService
                .ValidateMeasurementProtocolAsync(cancellationToken)
                .ConfigureAwait(false);

        return ToHttpResult(result);
    });

app.Run();

static IResult ToHttpResult<T>(GoogleAnalyticsOperationResult<T> result)
{
    if (result.Success)
    {
        return Results.Ok(result.Data);
    }

    int statusCode = result.StatusCode switch
    {
        HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
        HttpStatusCode.Unauthorized => StatusCodes.Status502BadGateway,
        HttpStatusCode.Forbidden => StatusCodes.Status502BadGateway,
        HttpStatusCode.NotFound => StatusCodes.Status502BadGateway,
        null => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status502BadGateway
    };

    return Results.Problem(
        title: result.ErrorCode,
        detail: result.ErrorMessage,
        statusCode: statusCode);
}
