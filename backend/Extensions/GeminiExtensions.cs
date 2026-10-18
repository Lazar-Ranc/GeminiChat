using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using GeminiChat.Api.Configuration;

namespace GeminiChat.Api.Extensions;

public static class GeminiExtensions
{
    public static void AddGemini(this IServiceCollection services)
    {
        // One Google client for the whole app (singleton): its HttpClient and connections
        // are reused by every request, and DI disposes it when the app shuts down.
        services.AddSingleton(sp =>
        {
            var gemini = sp.GetRequiredService<IOptions<GeminiSettings>>().Value;
            return new Client(apiKey: gemini.ApiKey);
        });

        // The chat endpoint talks to Gemini through IChatClient (Microsoft.Extensions.AI),
        // built on top of that same client. AddChatClient registers it as a singleton too.
        services.AddChatClient(sp =>
        {
            var gemini = sp.GetRequiredService<IOptions<GeminiSettings>>().Value;
            return sp.GetRequiredService<Client>().AsIChatClient(gemini.ChatModel);
        });
    }
}
