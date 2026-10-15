using System.Diagnostics;

namespace StudioCRM.Api.Diagnostics;

// Adds a Server-Timing header (visible in browser DevTools) and logs slow API calls,
// so slow saves can be split into network, server and database/Outlook time.
public sealed class RequestTimingMiddleware
{
    private const int SlowRequestMs = 1000;
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append("Server-Timing", $"app;dur={stopwatch.Elapsed.TotalMilliseconds:0}");
            return Task.CompletedTask;
        });

        await _next(context);

        var elapsed = stopwatch.ElapsedMilliseconds;
        if (elapsed >= SlowRequestMs && context.Request.Path.StartsWithSegments("/api"))
        {
            _logger.LogWarning("Slow request {Method} {Path} -> {StatusCode} in {ElapsedMs} ms",
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsed);
        }
    }
}
