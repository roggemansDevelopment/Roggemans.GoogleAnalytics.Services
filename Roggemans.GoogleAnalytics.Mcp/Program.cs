using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Roggemans.GoogleAnalytics.Mcp.Features.Mcp;
using Roggemans.GoogleAnalytics.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddGoogleAnalyticsServices(builder.Configuration);
builder.Services.AddSingleton<GoogleAnalyticsMcpSessionStore>();
builder.Services.AddScoped<GoogleAnalyticsMcpRequestHandler>();
builder.Services.Configure<GoogleAnalyticsMcpOptions>(
    builder.Configuration.GetSection(GoogleAnalyticsMcpOptions.SectionName));

var app = builder.Build();

var sseSessions = new ConcurrentDictionary<string, SseSession>();
var sseJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapGet("/", () => Results.Text("Roggemans GoogleAnalytics MCP is running."));

app.MapGet("/mcp", async (
        HttpContext context,
        GoogleAnalyticsMcpSessionStore sessionStore,
        IOptions<GoogleAnalyticsMcpOptions> options,
        ILogger<Program> logger) =>
    {
        if (!IsSseRequest(context.Request))
        {
            return Results.Json(new { status = "ok" });
        }

        if (!IsApiKeyValid(context.Request, options.Value))
        {
            return Results.Unauthorized();
        }

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        string sessionId = Guid.NewGuid().ToString("N");
        SseSession session = new();

        if (!sseSessions.TryAdd(sessionId, session))
        {
            logger.LogWarning("Failed to register SSE session {SessionId}.", sessionId);
            return Results.Problem("Unable to open SSE session.");
        }

        sessionStore.Register(sessionId);
        context.Response.Headers["Mcp-Session-Id"] = sessionId;

        string endpoint = $"{context.Request.PathBase}{context.Request.Path}?session_id={sessionId}";
        await WriteSseEventAsync(context.Response, "endpoint", endpoint, context.RequestAborted);

        using PeriodicTimer keepAliveTimer = new(TimeSpan.FromSeconds(15));

        try
        {
            while (!context.RequestAborted.IsCancellationRequested)
            {
                Task<string> readTask = session.Reader.ReadAsync(context.RequestAborted).AsTask();
                Task<bool> keepAliveTask = keepAliveTimer.WaitForNextTickAsync(context.RequestAborted).AsTask();

                Task completed = await Task.WhenAny(readTask, keepAliveTask);
                if (completed == readTask)
                {
                    string payload = await readTask;
                    await WriteSseEventAsync(context.Response, "message", payload, context.RequestAborted);
                }
                else if (await keepAliveTask)
                {
                    await WriteSseCommentAsync(context.Response, "keep-alive", context.RequestAborted);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            sseSessions.TryRemove(sessionId, out _);
            sessionStore.Remove(sessionId);
            session.Complete();
        }

        return Results.Empty;
    })
    .AllowAnonymous();

app.MapPost("/mcp", async (
        HttpContext context,
        GoogleAnalyticsMcpRequestHandler requestHandler,
        GoogleAnalyticsMcpSessionStore sessionStore,
        IOptions<GoogleAnalyticsMcpOptions> options,
        ILogger<Program> logger) =>
    {
        string? sessionId = GetSseSessionId(context.Request);
        bool hasSessionId = !string.IsNullOrWhiteSpace(sessionId);
        SseSession? sseSession = null;

        if (hasSessionId && !sseSessions.TryGetValue(sessionId!, out sseSession))
        {
            return Results.StatusCode(StatusCodes.Status410Gone);
        }

        if (!IsApiKeyValid(context.Request, options.Value))
        {
            return Results.Unauthorized();
        }

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(
                context.Request.Body,
                cancellationToken: context.RequestAborted);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid MCP JSON payload.");
            Dictionary<string, object?> errorResponse = BuildErrorResponse(null, -32600, "Invalid JSON payload.");
            return await WriteMcpResponseAsync(errorResponse, sseSession, context.RequestAborted);
        }

        using (document)
        {
            JsonElement request = document.RootElement;

            if (IsInitializedNotification(request))
            {
                return await WriteMcpResponseAsync(null, sseSession, context.RequestAborted);
            }

            IDictionary<string, object?> response = await requestHandler.HandleAsync(
                request,
                sessionId,
                options.Value,
                sessionStore,
                logger,
                context.RequestAborted);

            return await WriteMcpResponseAsync(response, sseSession, context.RequestAborted);
        }
    })
    .AllowAnonymous();

app.MapDelete("/mcp", (HttpRequest request, GoogleAnalyticsMcpSessionStore sessionStore) =>
    {
        string? sessionId = GetSseSessionId(request);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Results.BadRequest(new { error = "Missing session id." });
        }

        if (!sseSessions.TryRemove(sessionId, out SseSession? session))
        {
            return Results.NotFound();
        }

        sessionStore.Remove(sessionId);
        session.Complete();
        return Results.NoContent();
    })
    .AllowAnonymous();

app.Run();

static Dictionary<string, object?> BuildErrorResponse(JsonElement? id, int code, string message)
{
    return new Dictionary<string, object?>
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id.HasValue ? id.Value : null,
        ["error"] = new { code, message }
    };
}

async Task<IResult> WriteMcpResponseAsync(
    object? payload,
    SseSession? session,
    CancellationToken cancellationToken)
{
    if (payload is null)
    {
        return session is null ? Results.NoContent() : Results.Accepted();
    }

    if (session is null)
    {
        return Results.Json(payload);
    }

    string json = JsonSerializer.Serialize(payload, sseJsonOptions);
    if (!session.TryWrite(json))
    {
        await session.WriteAsync(json, cancellationToken);
    }

    return Results.Accepted();
}

static bool IsSseRequest(HttpRequest request)
{
    if (request.Query.TryGetValue("transport", out Microsoft.Extensions.Primitives.StringValues transport))
    {
        string transportValue = transport.ToString();
        if (string.Equals(transportValue, "sse", StringComparison.OrdinalIgnoreCase)
            || string.Equals(transportValue, "streamable_http", StringComparison.OrdinalIgnoreCase)
            || string.Equals(transportValue, "streamable-http", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    if (request.Query.TryGetValue("sse", out Microsoft.Extensions.Primitives.StringValues sse)
        && string.Equals(sse, "true", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    string accept = request.Headers.Accept.ToString();
    return accept.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);
}

static bool IsApiKeyValid(HttpRequest request, GoogleAnalyticsMcpOptions options)
{
    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        return true;
    }

    string? provided = request.Headers["X-Mcp-ApiKey"].FirstOrDefault();
    return string.Equals(provided, options.ApiKey, StringComparison.Ordinal);
}

static bool IsInitializedNotification(JsonElement request)
{
    if (!request.TryGetProperty("method", out JsonElement methodElement)
        || methodElement.ValueKind != JsonValueKind.String)
    {
        return false;
    }

    string? method = methodElement.GetString();
    if (!string.Equals(method, "initialized", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(method, "notifications/initialized", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    if (!request.TryGetProperty("id", out JsonElement idElement))
    {
        return true;
    }

    return idElement.ValueKind == JsonValueKind.Null;
}

static string? GetSseSessionId(HttpRequest request)
{
    string? headerSessionId = request.Headers["Mcp-Session-Id"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(headerSessionId))
    {
        return headerSessionId;
    }

    string? sessionId = request.Query["sessionId"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(sessionId))
    {
        return sessionId;
    }

    return request.Query["session_id"].FirstOrDefault();
}

static async Task WriteSseEventAsync(
    HttpResponse response,
    string eventName,
    string data,
    CancellationToken cancellationToken)
{
    await response.WriteAsync($"event: {eventName}\n", cancellationToken);

    foreach (string line in data.Replace("\r", string.Empty).Split('\n'))
    {
        await response.WriteAsync($"data: {line}\n", cancellationToken);
    }

    await response.WriteAsync("\n", cancellationToken);
    await response.Body.FlushAsync(cancellationToken);
}

static async Task WriteSseCommentAsync(
    HttpResponse response,
    string comment,
    CancellationToken cancellationToken)
{
    await response.WriteAsync($": {comment}\n\n", cancellationToken);
    await response.Body.FlushAsync(cancellationToken);
}

sealed class SseSession
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ChannelReader<string> Reader => _channel.Reader;

    public bool TryWrite(string payload) => _channel.Writer.TryWrite(payload);

    public ValueTask WriteAsync(string payload, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(payload, cancellationToken);

    public void Complete() => _channel.Writer.TryComplete();
}
