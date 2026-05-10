using System.Collections.Concurrent;

namespace Roggemans.GoogleAnalytics.Mcp.Features.Mcp;

public sealed class GoogleAnalyticsMcpSessionStore
{
    private readonly ConcurrentDictionary<string, TrackingContext> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _sessions.TryAdd(sessionId, new TrackingContext());
        }
    }

    public void Remove(string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _sessions.TryRemove(sessionId, out _);
        }
    }

    public TrackingContext GetOrCreate(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return new TrackingContext();
        }

        return _sessions.GetOrAdd(sessionId, _ => new TrackingContext());
    }

    public void SetContext(
        string? sessionId,
        string? clientId,
        string? userId,
        string? sessionTrackingId)
    {
        TrackingContext context = GetOrCreate(sessionId);
        context.ClientId = Normalize(clientId) ?? context.ClientId;
        context.UserId = Normalize(userId) ?? context.UserId;
        context.SessionId = Normalize(sessionTrackingId) ?? context.SessionId;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed class TrackingContext
{
    public string? ClientId { get; set; }

    public string? UserId { get; set; }

    public string? SessionId { get; set; }
}
