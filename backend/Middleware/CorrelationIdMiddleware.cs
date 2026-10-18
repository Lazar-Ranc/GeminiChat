using System.Diagnostics.CodeAnalysis;

namespace GeminiChat.Api.Middleware;

public class CorrelationIdMiddleware(ILogger<CorrelationIdMiddleware> logger) : IMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const int MaxLength = 64;

    // ─── ALTERNATIVE: publish the wire name, hide the storage detail ──────────
    // public const string HeaderName = "X-Correlation-Id";  // replaces CorrelationIdHeader.
    //                                                       // public: it's a wire-format fact
    //                                                       // that CORS and clients legitimately name.
    // private const string ItemKey = "CorrelationId";        // stays private: that we store it in
    //                                                       // Items is nobody else's business.
    //
    // /// <summary>The correlation ID for this request, or null if this middleware didn't run.</summary>
    // public static string? GetCorrelationId(HttpContext context) =>
    //     context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;
    //
    // Callers then ask for the ID, not for the key:
    //     CorrelationIdMiddleware.GetCorrelationId(ctx.HttpContext)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        //guard against log forging attacks by validating the incoming correlation id and rejecting any that contain non-ASCII letters or digits, or are too long.  If the incoming correlation id is invalid, we generate a new one and log a warning.
        var incoming = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        // var incoming = context.Request.Headers[HeaderName].FirstOrDefault(); //in case alternative is used
        var accepted = IsValid(incoming);
        var correlationId = accepted ? incoming! : Guid.NewGuid().ToString();

        // Sharing the same Correlation ID across two different places:
        // - context.Items: Our "internal backpack" that lives only for the duration of this HTTP request. Used by our code.
        // - Response.Headers: The "return envelope" for the client (e.g., Angular/React). If the client app crashes, 
        //   they can report this ID, allowing us to easily trace the exact error in our logs.
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        // context.Items[ItemKey] = correlationId;                 // in case we use the alternative
        // context.Response.Headers[HeaderName] = correlationId;




        //old version with dictionary
        // using (logger.BeginScope(new Dictionary<string, object>
        // {
        //     ["CorrelationId"] = correlationId
        // }))
        // 1. Structured Logging: String interpolation ($) is intentionally omitted to preserve semantic structure.
        //    This allows ILogger to parse "{CorrelationId}" as a distinct JSON property rather than a flattened string.
        // 2. Pipeline Scoping: By executing 'await next(context)' within this using block, the CorrelationId 
        //    is pushed into the logging context. All downstream components (controllers, services) will 
        //    automatically inherit this ID in their logs, enabling seamless distributed tracing.
        using (logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            if (!accepted && !string.IsNullOrEmpty(incoming))
            {
                // NOTE: log the LENGTH, not the value. Logging the rejected
                // value would reintroduce exactly the injection we just blocked.
                logger.LogWarning(
                    "Rejected malformed X-Correlation-Id of length {Length}; generated a new one.",
                    incoming.Length);
            }

            logger.LogInformation("Request {Path} assigned CorrelationId {CorrelationId}",
            context.Request.Path, correlationId);

            // Console.WriteLine($"IN   {GetType().Name}");
            await next(context);
            // Console.WriteLine($"OUT  {GetType().Name}");
        }
    }

    private static bool IsValid([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
            return false;

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                return false;
        }

        return true;
    }
}