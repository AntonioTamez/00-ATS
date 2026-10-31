using Clinic.Appointments;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinic.Billing;

public static class BillingSetup
{
    public static IServiceCollection AddBilling(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<InvoiceBook>();
        services.AddScoped<IFeeCharger, ChargeLateCancellationFee>();
        services.AddScoped<ListInvoices>();
        return services;
    }

    public static IEndpointRouteBuilder MapBilling(this IEndpointRouteBuilder app)
    {
        app.MapGet("/patients/{patientId:guid}/invoices", (Guid patientId, ListInvoices list) =>
            Results.Ok(list.Execute(patientId)));
        return app;
    }
}
