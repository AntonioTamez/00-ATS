namespace Clinic.Billing;

public sealed record Invoice(Guid Id, Guid PatientId, string Reason, decimal Amount, DateTimeOffset IssuedAt);

/// <summary>The clinic's invoices (in memory).</summary>
public sealed class InvoiceBook
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
