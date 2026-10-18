using System.Net;

namespace GeminiChat.Api.Exceptions;

/// <summary>
/// Exception thrown when the request is invalid or malformed.
/// Returns HTTP 400 Bad Request.
/// </summary>
public sealed class BadRequestException(string message)
    : AppException(message, HttpStatusCode.BadRequest);