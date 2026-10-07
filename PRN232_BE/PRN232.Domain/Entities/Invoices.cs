namespace PRN232.Domain.Entities;

public class Invoices
{
    public int InvoiceId { get; set; }
    public int ContractId { get; set; }
    public DateOnly InvoiceMonth { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = string.Empty;

    public Contracts? Contract { get; set; }
    public ICollection<InvoiceDetails> InvoiceDetails { get; set; } = new List<InvoiceDetails>();
    public ICollection<Payments> Payments { get; set; } = new List<Payments>();
}
