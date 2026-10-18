using System.Net;

namespace GeminiChat.Api.Exceptions;

/// <summary>
/// Base exception class for all application-specific exceptions.
/// Carries an HttpStatusCode to determine the appropriate response status.
/// </summary>
public abstract class AppException(string message, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
    : Exception(message)
{
  public HttpStatusCode StatusCode { get; } = statusCode;
}