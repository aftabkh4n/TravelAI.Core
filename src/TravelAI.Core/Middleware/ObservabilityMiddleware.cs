using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TravelAI.Core.Middleware;

public sealed class TravelAIObservabilityMiddleware(RequestDelegate next, ILogger<TravelAIObservabilityMiddleware> logger)
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? Guid.NewGuid().ToString("N")[..12];
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        context.Items["CorrelationId"] = correlationId;

        var sw = Stopwatch.StartNew();
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.Value ?? string.Empty
        });

        try { await next(context); }
        finally
        {
            sw.Stop();
            var aiService = context.Items.TryGetValue("AiServiceUsed", out var svc) ? svc?.ToString() : null;
            logger.LogInformation("HTTP {Method} {Path} → {StatusCode} in {ElapsedMs}ms{AiSuffix}",
                context.Request.Method, context.Request.Path, context.Response.StatusCode,
                sw.ElapsedMilliseconds, aiService is not null ? $" [AI:{aiService}]" : string.Empty);
        }
    }
}

public sealed class AiRateLimitMiddleware(RequestDelegate next, ILogger<AiRateLimitMiddleware> logger)
{
    private static readonly Dictionary<string, (int Count, DateTime Window)> _windows = new();
    private static readonly Lock _lock = new();
    private const int MaxRequestsPerMinute = 10;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/itinerary") &&
            !context.Request.Path.StartsWithSegments("/api/search"))
        {
            await next(context);
            return;
        }

        var clientId = context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

        if (!IsAllowed(clientId))
        {
            logger.LogWarning("Rate limit exceeded for {ClientId}", clientId);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = "60";
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Too many AI requests. Retry after 60 seconds.\"}");
            return;
        }

        await next(context);
    }

    private static bool IsAllowed(string clientId)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_windows.TryGetValue(clientId, out var state))
            {
                if ((now - state.Window).TotalSeconds > 60) { _windows[clientId] = (1, now); return true; }
                if (state.Count >= MaxRequestsPerMinute) return false;
                _windows[clientId] = (state.Count + 1, state.Window);
                return true;
            }
            _windows[clientId] = (1, now);
            return true;
        }
    }
}
