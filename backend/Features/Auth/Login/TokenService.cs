using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using GeminiChat.Api.Configuration;


namespace GeminiChat.Api.Features.Auth.Login;

public class TokenService(IOptions<JwtSettings> jwtSettings) : ITokenService
{
    /*
        if we weren't using JwtSettings class it would work but it would look like this:
        string key = builder.Configuration["JwtSettings:Key"];
        string issuer = builder.Configuration["JwtSettings:Issuer"];
        string audience = builder.Configuration["JwtSettings:Audience"];
        int expiry = int.Parse(builder.Configuration["JwtSettings:ExpiryMinutes"]);  // note: have to parse!
        This works. No JwtSettings class needed. But look at the problems, because they're exactly what the class solves:

        Stringly-typed keys. "JwtSettings:Key" is a magic string. Typo it — "JwtSetting:Key" — and you get a silent null at runtime, not a compile error. No safety.
        Everything's a string. Config is text, so ExpiryMinutes comes out as "60", and you have to manually int.Parse it every time. Error-prone.
        Scattered and repeated. Every place that needs a setting repeats the key-path lookup. Change the section name and you hunt down every string.
        No grouping. The four related settings aren't bundled into one thing you can pass around; they're four loose lookups.

    */
    private readonly JwtSettings _settings = jwtSettings.Value;

    public (string Token, DateTime ExpiresAt) CreateToken(string userName)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        // Claims are the pieces of information we store inside the token.
        var claims = new List<Claim>
        {
            // if we were to write just ClaimType instead of JwtRegisteredClaimNames we would get:
            // If you use ClaimTypes.Name, the actual string injected into your token isn't just "name". It is this massive, ugly URL:
            // "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
            // To make things even more confusing, if you use the older JwtSecurityTokenHandler to create or read tokens, 
            // Microsoft tries to be "helpful." Behind the scenes, it intercepts modern claims like "sub" and automatically overwrites them
            //  with the massive legacy URLs like "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier".
            // This often breaks frontend applications that are trying to read the token.

            new(JwtRegisteredClaimNames.Sub, userName),
            // new(JwtRegisteredClaimNames.Email, user.Email!),
            // new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // One "role" claim per role the user has.
        // claims.AddRange(roles.Select(role => new Claim("role", role)));

        // The signing key proves the token came from us and was not tampered with.
        var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(_settings.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha512);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = credentials
        };

        // JsonWebTokenHandler is the modern, faster handler that replaces JwtSecurityTokenHandler.
        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        return (token, expiresAt);
    }
}