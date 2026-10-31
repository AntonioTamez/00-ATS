using Clinic.Billing.Domain;

namespace Clinic.Billing.Application;

public sealed record InvoiceDto(Guid Id, Guid PatientId, string Reason, decimal Amount, DateTimeOffset IssuedAt);

public interface IInvoiceRepository
{
    void Add(Invoice invoice);
    IReadOnlyList<Invoice> ForPatient(Guid patientId);
}

internal static class InvoiceMapping
{
    public static InvoiceDto ToDto(Invoice i) => new(i.Id, i.PatientId, i.Reason, i.Amount, i.IssuedAt);
}

/// <summary>Use case: a patient cancelled too late, so the clinic issues a fee.</summary>
public sealed class ChargeLateCancellationFee(IInvoiceRepository invoices, TimeProvider clock)
{
    public InvoiceDto Execute(Guid patientId, Guid appointmentId)
    {
        var invoice = Invoice.ForLateCancellation(patientId, appointmentId, clock.GetUtcNow());
        invoices.Add(invoice);
        return InvoiceMapping.ToDto(invoice);
    }
}

/// <summary>Use case: see what a patient has been invoiced.</summary>
public sealed class ListInvoices(IInvoiceRepository invoices)
{
    public IReadOnlyList<InvoiceDto> Execute(Guid patientId) =>
        invoices.ForPatient(patientId).Select(InvoiceMapping.ToDto).ToList();
}
