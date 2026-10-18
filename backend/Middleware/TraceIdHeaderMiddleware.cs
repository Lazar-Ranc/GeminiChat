using System.Diagnostics;

namespace GeminiChat.Api.Middleware;

public class TraceIdHeaderMiddleware(RequestDelegate next, ILogger<TraceIdHeaderMiddleware> logger)
{
  private const string TraceIdHeader = "X-Trace-Id";

  public async Task InvokeAsync(HttpContext context)
  {
    var activity = Activity.Current;

    if (activity is not null)
    {
      context.Response.Headers[TraceIdHeader] = activity.TraceId.ToString();
    }
    else
    {
      logger.LogWarning("No current Activity; {Header} was not set.", TraceIdHeader);
    }

    // Console.WriteLine($"IN   {GetType().Name}");
    await next(context);
    // Console.WriteLine($"OUT  {GetType().Name}");
  }
}