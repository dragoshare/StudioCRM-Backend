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
        if (!context.Request.Path.StartsWithSegments("/api"))
            return;

        // A browser that gave up waiting (frontend timeout, closed tab) still lets the server finish the save,
        // so "failed" in the UI can mean the record was created anyway.
        if (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning("Client disconnected before response {Method} {Path} -> {StatusCode} after {ElapsedMs} ms",
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsed);
        }
        else if (context.Response.StatusCode >= 400 && !HttpMethods.IsGet(context.Request.Method))
        {
            _logger.LogWarning("Failed request {Method} {Path} -> {StatusCode} in {ElapsedMs} ms",
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsed);
        }
        else if (elapsed >= SlowRequestMs)
        {
            _logger.LogWarning("Slow request {Method} {Path} -> {StatusCode} in {ElapsedMs} ms",
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsed);
        }
    }
}
