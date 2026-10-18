using Google.GenAI.Types;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GeminiChat.Api.Features;

public static class GenerateText
{

    public record ChatHistoryDto(
        [Required(ErrorMessage = "Messages are required.")]
        [MinLength(1, ErrorMessage = "At least one message is required.")]
        [MaxLength(100, ErrorMessage = "A conversation cannot exceed 100 messages.")]
        List<MessageDto> Messages
    );

    public record MessageDto(
        [Required(ErrorMessage = "Role is required.")]
        string Role,

        // AI replies are sent back as part of the history, so this limit has to fit long answers, not just prompts.
        [Required(ErrorMessage = "Content is required.")]
        [MaxLength(20000, ErrorMessage = "A message cannot exceed 20000 characters.")]
        string Content
    );

    public static void MapGenerateText(this WebApplication app)
    {
        // chatClient comes from DI: one shared instance registered in Extensions/GeminiExtensions.cs.
        app.MapPost("/generatetext", async (ChatHistoryDto chatHistory, IChatClient chatClient) =>
            {
                List<ChatMessage> chatMessage = [];

                foreach (var msg in chatHistory.Messages)
                {
                    var role = (msg.Role == "user") ? ChatRole.User : ChatRole.Assistant;
                    chatMessage.Add(new(role, msg.Content));
                }

                ChatResponse response = await chatClient.GetResponseAsync(chatMessage);

                var responseDto = new { answerText = response.Text };
                return Results.Ok(responseDto);
                // Without the DTO, frontend line has to be data.messages[0].contents[0].text — ugly, and coupled to MEAI's exact nesting (breaks if you swap SDKs, exactly the coupling problem).
                // With the DTO,  frontend line is the clean data.answerText — and it works because you shaped the JSON to have that property.
            })
            //Further customasable using the OpenAPI attributes, for example:
            //         .WithName("GetGreeting")  // Assigns an operation ID
            // .WithSummary("Greet the user by name")  // Provides a short summary for the endpoint
            // .WithDescription("This endpoint takes a name as input and returns a personalized greeting.")  // Longer description
            // .WithTags("Greetings")  // Adds tags to categorize the endpoint
            // .Produces(200, typeof(object))  // Defines a successful response with the expected type
            // .Produces(400)  // Defines possible HTTP 400 responses
            // .ProducesProblem(500)  // Defines a problem details response for failures
            .RequireAuthorization();  // Requires authentication for the endpoint


    }
}