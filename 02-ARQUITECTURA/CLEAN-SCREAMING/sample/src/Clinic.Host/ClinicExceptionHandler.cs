using Clinic.SharedKernel;
using Microsoft.AspNetCore.Diagnostics;

namespace Clinic.Host;

/// <summary>The one place that turns the use cases' failures into HTTP.</summary>
internal sealed class ClinicExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            RuleViolationException => (StatusCodes.Status422UnprocessableEntity, "A clinic rule was broken"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            _ => (0, ""),
        };
        if (status == 0) return false;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { title, detail = exception.Message, status }, cancellationToken);
        return true;
    }
}
