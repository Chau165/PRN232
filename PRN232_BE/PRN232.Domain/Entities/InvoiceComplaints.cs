namespace PRN232.Domain.Entities;

public class InvoiceComplaints
{
    public int ComplaintId
    {
        get; set;
    }
    public int InvoiceId
    {
        get; set;
    }
    public int CustomerId
    {
        get; set;
    }
    public string FeeType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt
    {
        get; set;
    }

    public Invoices? Invoice
    {
        get; set;
    }
    public Users? Customer
    {
        get; set;
    }
}
