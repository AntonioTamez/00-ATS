using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Api.Common;

/// <summary>A business rule was violated (raised by <see cref="Ticket"/>).</summary>
public sealed class DomainException(string message) : Exception(message);

/// <summary>The request itself is malformed (bad paging, unknown filter...).</summary>
public sealed class RequestValidationException(string message) : Exception(message);

public sealed class NotFoundException(string message) : Exception(message);

/// <summary>One place that turns errors into HTTP, so slices only throw.</summary>
internal sealed class ErrorHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Business rule violated"),
            RequestValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            _ => (0, string.Empty)
        };
        if (status == 0) return false;

        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = exception.Message }
        });
    }
}
