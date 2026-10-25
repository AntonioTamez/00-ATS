using Hotel.Application;
using Hotel.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hotel.Api;

/// <summary>Translates domain and application errors into HTTP. The inner layers know nothing about status codes.</summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Business rule violated"),
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
