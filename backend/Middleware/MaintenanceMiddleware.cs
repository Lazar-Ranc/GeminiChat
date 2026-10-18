namespace GeminiChat.Api.Middleware;

public class MaintenanceMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Console.WriteLine($"IN   {GetType().Name}");
        var isMaintenanceMode = configuration.GetValue<bool>("MaintenanceMode");
        if (isMaintenanceMode)
        {
            Console.WriteLine($"STOP {GetType().Name}");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                Status = 503,
                Message = "Service is under maintenance. Please try again later."
            });
            return; // Short-circuit — do not call next middleware
        }

        await next(context);
        // Console.WriteLine($"OUT  {GetType().Name}");
    }
}