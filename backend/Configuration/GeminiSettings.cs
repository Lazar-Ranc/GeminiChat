namespace GeminiChat.Api.Configuration;

public class GeminiSettings
{
    public string ApiKey { get; set; } = string.Empty;      // kept in user-secrets, not in appsettings.json
    public string ChatModel { get; set; } = string.Empty;   // model used by /generatetext
}
