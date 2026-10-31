namespace Clinic.Billing;

/// <summary>Use case: see what a patient has been invoiced.</summary>
public sealed class ListInvoices(InvoiceBook invoices)
{
    public IReadOnlyList<Invoice> Execute(Guid patientId) => invoices.ForPatient(patientId);
}
