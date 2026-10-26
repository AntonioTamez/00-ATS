using Microsoft.AspNetCore.Diagnostics;

namespace Clinic;

/// <summary>A business rule was broken. Raised by the use cases, which know nothing about HTTP.</summary>
public sealed class RuleViolationException(string message) : Exception(message);

public sealed class NotFoundException(string message) : Exception(message);

/// <summary>The one place that translates the use cases' failures into HTTP. It is framework code, so it lives at the edge.</summary>
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

internal static class ErrorHandlingSetup
{
    public static IServiceCollection AddClinicErrorHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<ClinicExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }
}
