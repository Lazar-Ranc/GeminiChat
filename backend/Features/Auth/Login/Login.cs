using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using GeminiChat.Api.Configuration;

namespace GeminiChat.Api.Features.Auth.Login;

public static class Login
{
    public record LoginRequestDto
    {
        [Required(ErrorMessage = "Username is required.")]
        public string UserName { get; init; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; init; } = null!;

    };     // DTO in
    public record LoginResponseDto(
        string Token,
        DateTime ExpiresAt
    );    // DTO out
    //token creating here because its used for every log in 

    public static void MapLogin(this WebApplication app)
    {
        app.MapPost("/login", LoginAsync);
    }

    private static IResult LoginAsync(
    LoginRequestDto request,
    IOptions<AuthCredentials> authOptions,
    ITokenService tokenService)
    {
        var credentials = authOptions.Value;

        // Same response for "no such user" and "wrong password" so we don't leak which username exist.
        if (request.UserName != credentials.Username || !HashProvider.Verify(request.Password, credentials.PasswordHash))
        {
            return Results.Unauthorized();
        }


        var (token, expiresAt) = tokenService.CreateToken(request.UserName);

        return Results.Ok(new LoginResponseDto(token, expiresAt));
    }
}