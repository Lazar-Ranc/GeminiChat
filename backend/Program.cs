using GeminiChat.Api.Configuration;
using GeminiChat.Api.Features;
using GeminiChat.Api.Features.Auth;
using GeminiChat.Api.Features.Auth.Login;
using Google.GenAI;
using Google.GenAI.Types;
using GeminiChat.Api.Middleware;
using GeminiChat.Api.Exceptions;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using System.Diagnostics;
using Serilog;
using GeminiChat.Api.Extensions;


//creates a temporary logger that captures any errors during startup - before appsettings.json is even loaded.
//  Without this, startup crashes vanish silently.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();
try
{
    //this registers framework services, including Ilogger<T> and IConfiguration, IserviceProvider, IOptions<T> etc.  It also registers the DI container itself, so you can inject IServiceProvider if you want to resolve services manually.
    var builder = WebApplication.CreateBuilder(args);

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 1 * 1024 * 1024;   // 1 MB
        options.Limits.MaxRequestHeadersTotalSize = 16 * 1024; // 16 KB
        options.Limits.MaxRequestHeaderCount = 50;
        options.Limits.MaxConcurrentConnections = 1000;        // default is unlimited
    });

    builder.Services.AddExceptionHandling();



    builder.Services.AddSerilog((services, lc) => lc
          .ReadFrom.Configuration(builder.Configuration)
          .ReadFrom.Services(services));

    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            // No name passed to AddOpenApi, so the document is "v1" (served at /openapi/v1.json),
            // which is also the document Scalar loads by default. "version" follows the name.
            document.Info.Version = context.DocumentName;
            document.Info.Title = "GeminiChat.Api - Gemini API proxy";
            document.Info.Description =
                "A personal ASP.NET Core Minimal API that proxies Google Gemini for multi-turn chat " +
                "through Microsoft.Extensions.AI (IChatClient). Uses vertical slice architecture, " +
                "JWT authentication (HMAC-SHA512) and PBKDF2/SHA-512 password hashing.";

            document.Info.Contact = new OpenApiContact
            {
                Name = "Lazar Ranc",
                Email = "lazar.j.ranc@gmail.com",
                Url = new Uri("https://github.com/Lazar-Ranc")
            };

            // Servers can be null in Microsoft.OpenApi 2.x, so replace the list instead of clearing it.
            document.Servers =
            [
                new OpenApiServer
                {
                    Url = "/",
                    Description = "Current host"
                }
            ];

            return Task.CompletedTask;
        });
    });

    builder.Services.AddValidation(); // adds validation for minimal APIs, using data annotations



    // --- CORS CONFIGURATION ---
    // Configures the cross-origin policy to allow communication with the frontend application.
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://127.0.0.1:5500")

                  // 1. Permitted Incoming Headers: What the client is allowed to send
                  .WithHeaders("Authorization", "Content-Type", "Accept")

                  // 2. Permitted Actions: Allow any HTTP verb (GET, POST, PUT, DELETE, etc.)
                  .AllowAnyMethod()

                  // 3. Exposed Outgoing Headers: Make our custom trace IDs visible to frontend JavaScript
                  .WithExposedHeaders("X-Correlation-Id", "X-Trace-Id");
        });
    });
    // ALTERNATIVE — no retyped strings; each middleware publishes its own header name.
    // Needs TraceIdHeaderMiddleware.TraceIdHeader made public too (it's private today):
    // .AllowAnyMethod().WithExposedHeaders(CorrelationIdMiddleware.HeaderName,
    //                                      TraceIdHeaderMiddleware.HeaderName)));


    // --- JWT CONFIGURATION ---

    // Step 1: Register the configuration mapping in the DI container.
    // This tells the framework: "When a component asks for IOptions<JwtSettings>, 
    // read the 'JwtSettings' section from appsettings.json and bind it to the C# class."
    // Note: This only registers the 'recipe'. The actual binding happens lazily.
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

    // Step 2: Register our custom authentication and authorization services.
    // We no longer pass IConfiguration here. The extension will fetch IOptions via DI when needed.
    builder.Services.AddJwtAuth();

    builder.Services.AddScoped<ITokenService, TokenService>();

    // --- LOGIN CREDENTIALS ---
    // Same Options pattern as JwtSettings: Login.cs asks for IOptions<AuthCredentials>
    // instead of reading config["AuthCredentials:..."] strings.
    builder.Services.Configure<AuthCredentials>(builder.Configuration.GetSection("AuthCredentials"));

    // --- GEMINI ---
    // Same Options pattern: the API key (user-secrets) and the model names (appsettings.json)
    // bind to GeminiSettings.
    builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection("Gemini"));

    // Registers one shared Gemini client and the IChatClient built on it (Extensions/GeminiExtensions.cs).
    builder.Services.AddGemini();

    builder.Services.AddAuthorization();

    builder.Services.AddTransient<CorrelationIdMiddleware>(); // Register IMiddleware implementations with DI



    var app = builder.Build();

    app.UseCorrelationId();     // outermost — everything below gets an ID, spans multiple http requests
    app.UseTraceIdHeader();  // this exposes the trace ID to the client, so they can correlate logs with their request
    // app.UseSerilogRequestLogging();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };

        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            // Server faults first. ex is normally null here — UseExceptionHandler sits
            // below us and swallows it — but it arrives non-null if that middleware
            // rethrows, so both signals are checked.
            if (ex is not null || httpContext.Response.StatusCode >= 500)
                return Serilog.Events.LogEventLevel.Error;

            // 4xx is the client's mistake, not ours: visible, but not an alert.
            if (httpContext.Response.StatusCode >= 400)
                return Serilog.Events.LogEventLevel.Warning;

            // Inert until health checks are registered.
            if (httpContext.Request.Path.StartsWithSegments("/health"))
                return Serilog.Events.LogEventLevel.Verbose;

            return elapsed > 500   // milliseconds
                ? Serilog.Events.LogEventLevel.Warning
                : Serilog.Events.LogEventLevel.Information;
        };
    });
    app.UseExceptionHandling(); // logs exceptions and returns a problem details response
    // app.UseRequestLogging(); // logs every response, including rejected ones
    app.UseCors();         // must run before anything that short-circuits
    app.UseMaintenance();       // now it's safe to bail out

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options
            .WithTheme(ScalarTheme.Kepler);
            // .HideDarkModeToggle()
        });

        app.MapGet("/test", (ILogger<Program> logger) =>
         {
             int x = 123;
             logger.LogInformation("This is a test log message. Value of x: {X}", x);
             logger.LogInformation("Inside the endpoint");
             return "ok";
         });


        app.MapGet("/boom", () =>
        { throw new NotFoundException("Chat", 42); });

        app.MapGet("/crash", () => { throw new InvalidOperationException("boom"); });
    }


    app.UseAuthentication();    // read & validate the token (who are you?)
    app.UseAuthorization();     // enforce the gate (are you allowed?)

    app.MapGenerateText();
    app.MapLogin();




    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Server terminated unexpectedly.");
}
finally
{
    //guarantees all buffered log events are written before the process exits.
    Log.CloseAndFlush();
}


