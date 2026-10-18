using GeminiChat.Api.Exceptions;
using Google.GenAI;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GeminiChat.Api.Exceptions;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(
      HttpContext httpContext,
      Exception exception,
      CancellationToken cancellationToken)
  {
    // the response was cleared before we got here, so put the headers back.
    if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId) && correlationId is string id)
      httpContext.Response.Headers["X-Correlation-Id"] = id;


    var activity = Activity.Current;
    if (activity is not null)
      httpContext.Response.Headers["X-Trace-Id"] = activity.TraceId.ToString();

    // ─── ALTERNATIVE for the two blocks above (needs: using GeminiChat.Api.Middleware;) ───
    // Each middleware publishes its own header name, and the correlation ID comes
    // from an accessor instead of this class knowing that Items is the storage:
    //
    // var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext);
    // if (correlationId is not null)
    //   httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
    //
    // if (Activity.Current is { } activity)
    //   httpContext.Response.Headers[TraceIdHeaderMiddleware.HeaderName] = activity.TraceId.ToString();
    // ─────────────────────────────────────────────────────────────────────────────────
    logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}",
    activity?.TraceId.ToString() ?? httpContext.TraceIdentifier);
    // NOTE: this logs TraceIdentifier ("0HNO26IIG8TJ5:00000001"), but the body and
    // the X-Trace-Id header now carry Activity.Current.TraceId (32 hex chars). A user
    // reporting the ID from the error body cannot be found with this log line.
    // logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}",
    //     Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier);

    var (statusCode, title) = MapException(exception);

    //assign statuscode to the response
    httpContext.Response.StatusCode = statusCode;

    var problemDetails = new ProblemDetails
    {
      Status = statusCode,
      Title = title,
      Type = GetProblemType(statusCode),
      // Instance = httpContext.Request.Path,
      Detail = GetSafeErrorMessage(exception, httpContext)
    };

    // problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
    // problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

    return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
    {
      HttpContext = httpContext,
      ProblemDetails = problemDetails
    });
  }

  private static (int StatusCode, string Title) MapException(Exception exception) => exception switch
  {
    AppException appEx => ((int)appEx.StatusCode, appEx.Message),
    ArgumentNullException => (StatusCodes.Status400BadRequest, "Invalid argument provided"),
    ArgumentException => (StatusCodes.Status400BadRequest, "Invalid argument provided"),
    UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
    // The request body couldn't be read (e.g. malformed JSON). In Development, Minimal APIs throw this
    // instead of answering 400 directly, so keep its status code instead of turning it into a 500.
    BadHttpRequestException badRequest => (badRequest.StatusCode, "Invalid request"),
    // Gemini answers 503 when the model is overloaded ("high demand"). That's temporary,
    // so tell the caller to retry instead of reporting it as a bug in our server.
    ServerError { StatusCode: 503 } => (StatusCodes.Status503ServiceUnavailable, "The AI service is busy, try again in a moment"),
    _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
  };

  private static string GetProblemType(int statusCode) => statusCode switch
  {
    400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    401 => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
    403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
    404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
    409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
    503 => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
    _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
  };

  private static string? GetSafeErrorMessage(Exception exception, HttpContext context)
  {
    var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
    if (env.IsDevelopment())
    {
      return exception.Message;
    }
    return exception is AppException ? exception.Message : null;
  }
}