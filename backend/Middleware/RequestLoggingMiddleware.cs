using System.Diagnostics;
namespace GeminiChat.Api.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // var startTime = DateTime.UtcNow;
        var start = Stopwatch.GetTimestamp();
        logger.LogInformation("Incoming {Method} {Path}", context.Request.Method, context.Request.Path);

        // Console.WriteLine($"IN   {GetType().Name}");
        await next(context);
        // Console.WriteLine($"OUT  {GetType().Name}");

        // var elapsed = DateTime.UtcNow - startTime;
        var elapsed = Stopwatch.GetElapsedTime(start);
        logger.LogInformation("Completed {Method} {Path} with {StatusCode} in {Elapsed}ms",
            context.Request.Method, context.Request.Path, context.Response.StatusCode, elapsed.TotalMilliseconds);
    }
}