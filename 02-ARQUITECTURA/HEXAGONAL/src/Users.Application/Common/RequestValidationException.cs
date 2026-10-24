namespace Users.Application.Common;

/// <summary>Raised when an application request violates its input rules. Adapters map it to their error format.</summary>
public sealed class RequestValidationException : Exception
{
    public RequestValidationException()
    {
    }

    public RequestValidationException(string message)
        : base(message)
    {
    }

    public RequestValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
