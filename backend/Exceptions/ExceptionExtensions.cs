using System.Diagnostics;


namespace GeminiChat.Api.Exceptions;

public static class ExceptionExtensions
{
  public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
  {
    // REQUIRED: registers IProblemDetailsService, which GlobalExceptionHandler
    // takes in its constructor. Without this line the handler cannot even be
    // constructed and you get a bare 500 with no body.
    services.AddProblemDetails(options =>
    {
      // Runs for EVERY ProblemDetails response — not just exceptions. That
      // includes the framework's own 404s, 405s and the 400s from
      // AddValidation(), which never reach GlobalExceptionHandler.
      options.CustomizeProblemDetails = context =>
          {
            // Same value TraceIdHeaderMiddleware puts in the X-Trace-Id header,
            // so the body and the header finally agree.
            context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;

            // CorrelationIdMiddleware stashed this in Items on the way down.
            if (context.HttpContext.Items.TryGetValue("CorrelationId", out var correlationId))
              context.ProblemDetails.Extensions["correlationId"] = correlationId;

            context.ProblemDetails.Extensions["timestamp"] = DateTime.UtcNow;

            // showing on which route the error happened (e.g., GET /api/data)
            context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
          };
    });

    // Registration order here IS the chain order: first registered gets first
    // refusal at an exception. One handler for now, so nothing to decide yet.
    services.AddExceptionHandler<GlobalExceptionHandler>();
    // builder.Services.AddExceptionHandler<NotFoundExceptionHandler>(); // for later

    return services;
  }

  public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
      => app.UseExceptionHandler();
}