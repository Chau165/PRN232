namespace PRN232.Domain.Entities;

public class InvoiceDetails
{
    public int InvoiceDetailId { get; set; }
    public int InvoiceId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public Invoices? Invoice { get; set; }
}
