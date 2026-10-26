using Clinic.Appointments.Application;
using Clinic.Billing.Application;
using Clinic.Billing.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinic.Billing.Adapters;

/// <summary>OUTBOUND adapter: the invoice book, in memory.</summary>
public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly object _gate = new();
    private readonly List<Invoice> _invoices = [];

    public void Add(Invoice invoice)
    {
        lock (_gate) _invoices.Add(invoice);
    }

    public IReadOnlyList<Invoice> ForPatient(Guid patientId)
    {
        lock (_gate) return _invoices.Where(i => i.PatientId == patientId).OrderBy(i => i.IssuedAt).ToList();
    }
}

/// <summary>
/// Answers the port Appointments defined ("charge a late-cancellation fee") by running Billing's own use case.
/// Appointments' inner circle never learns that Billing exists.
/// </summary>
public sealed class FeeChargerAdapter(ChargeLateCancellationFee charge) : IFeeCharger
{
    public void ChargeLateCancellation(Guid patientId, Guid appointmentId) => charge.Execute(patientId, appointmentId);
}

public static class BillingModule
{
    public static IServiceCollection AddBilling(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IInvoiceRepository, InMemoryInvoiceRepository>();
        services.AddScoped<IFeeCharger, FeeChargerAdapter>();
        services.AddScoped<ChargeLateCancellationFee>();
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
