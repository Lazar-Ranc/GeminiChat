namespace GeminiChat.Api.Configuration;

public class AuthCredentials
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;   // the HASH, not the password 
}