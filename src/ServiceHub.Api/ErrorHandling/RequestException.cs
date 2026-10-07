namespace ServiceHub.Api.ErrorHandling;

// Expected application failures translated to safe ProblemDetails by the existing handler.
public sealed class RequestException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
