using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;
using GeminiChat.Api.Configuration;

namespace GeminiChat.Api.Extensions;

public static class AuthenticationExtensions
{
    public static void AddJwtAuth(this IServiceCollection services)
    {
        // 1. Register the authentication workers (Handlers) in the DI container.
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(); // Empty here! We configure the validation parameters separately below.

        // 2. Configure the JwtBearerOptions lazily using the Options pattern.
        // NOTE: This lambda does NOT execute at startup. It is registered in the DI container.
        // When the first HTTP request hits app.UseAuthentication(), the DI container creates the JwtBearerHandler,
        // fetches IOptions<JwtSettings>, and executes this lambda to apply the validation rules.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwtOpts) =>
            {
                // We extract the strongly-typed C# object provided by the DI container.
                // This eliminates magic strings and ensures both our TokenService and Middleware 
                // share the exact same configuration instance in memory.
                var jwt = jwtOpts.Value;

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    // Using the strongly-typed properties instead of config.GetValue<string>("...")
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwt.Key)),
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = "role"
                };
            });
    }
}