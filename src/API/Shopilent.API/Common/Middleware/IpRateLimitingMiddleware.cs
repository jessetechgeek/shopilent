using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Shopilent.API.Common.Configuration;

namespace Shopilent.API.Common.Middleware;

public sealed class IpRateLimitingMiddleware
{
    private sealed class WindowCounter
    {
        public long WindowStartUnix { get; set; }
        public int Count { get; set; }
        public object SyncRoot { get; } = new();
    }

    private static readonly ConcurrentDictionary<string, WindowCounter> Counters = new();
    private static long _lastCleanupUnix;
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<RateLimitingOptions> _optionsMonitor;

    public IpRateLimitingMiddleware(RequestDelegate next, IOptionsMonitor<RateLimitingOptions> optionsMonitor)
    {
        _next = next;
        _optionsMonitor = optionsMonitor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            await _next(context);
            return;
        }

        var apiPrefix = NormalizePrefix(options.ApiPrefix);
        if (!context.Request.Path.StartsWithSegments(apiPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var isAuthPath = IsVersionedAuthPath(context.Request.Path);
        var policyName = isAuthPath ? "auth" : "normal";
        var policy = isAuthPath ? options.Auth : options.Normal;

        var permitLimit = Math.Max(1, policy.PermitLimit);
        var windowSeconds = Math.Max(1, policy.WindowSeconds);

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStartUnix = nowUnix - (nowUnix % windowSeconds);
        var windowResetUnix = windowStartUnix + windowSeconds;
        var retryAfterSeconds = Math.Max(0, windowResetUnix - nowUnix);

        MaybeCleanupStaleCounters(nowUnix, windowSeconds);

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var counterKey = $"{policyName}:{ipAddress}";
        var counter = Counters.GetOrAdd(counterKey, _ => new WindowCounter
        {
            WindowStartUnix = windowStartUnix,
            Count = 0
        });

        var accepted = false;
        var remaining = 0;

        lock (counter.SyncRoot)
        {
            if (counter.WindowStartUnix != windowStartUnix)
            {
                counter.WindowStartUnix = windowStartUnix;
                counter.Count = 0;
            }

            if (counter.Count < permitLimit)
            {
                counter.Count++;
                accepted = true;
                remaining = Math.Max(0, permitLimit - counter.Count);
            }
        }

        context.Response.Headers["X-RateLimit-Limit"] = permitLimit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = windowResetUnix.ToString();

        if (accepted)
        {
            await _next(context);
            return;
        }

        context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new
        {
            succeeded = false,
            message = "Too many requests. Please try again later.",
            statusCode = StatusCodes.Status429TooManyRequests,
            errors = new[] { "Rate limit exceeded." }
        });

        await context.Response.WriteAsync(payload);
    }

    private static bool IsVersionedAuthPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is null || segments.Length < 3)
            return false;

        return segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
               && IsVersionSegment(segments[1])
               && segments[2].Equals("auth", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVersionSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment.Length < 2 || segment[0] != 'v')
            return false;

        for (var i = 1; i < segment.Length; i++)
        {
            if (!char.IsDigit(segment[i]))
                return false;
        }

        return true;
    }

    private static PathString NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return "/api";

        var normalized = prefix.StartsWith('/') ? prefix : $"/{prefix}";
        return normalized.EndsWith('/') ? normalized[..^1] : normalized;
    }

    private static void MaybeCleanupStaleCounters(long nowUnix, int windowSeconds)
    {
        // Cleanup runs at most once per window to avoid unbounded memory growth.
        if (Interlocked.Read(ref _lastCleanupUnix) + windowSeconds > nowUnix)
            return;

        Interlocked.Exchange(ref _lastCleanupUnix, nowUnix);
        var staleBefore = nowUnix - (windowSeconds * 2L);

        foreach (var kvp in Counters)
        {
            if (kvp.Value.WindowStartUnix < staleBefore)
            {
                Counters.TryRemove(kvp.Key, out _);
            }
        }
    }
}
