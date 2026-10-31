namespace PRN232.Domain.Entities;

public class InvoiceDetails
{
    public int InvoiceDetailId
    {
        get; set;
    }
    public int InvoiceId
    {
        get; set;
    }
    public string ItemType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount
    {
        get; set;
    }
    public int? ServiceId
    {
        get; set;
    }
    public int? RateId
    {
        get; set;
    }
    public decimal? Quantity
    {
        get; set;
    }
    public decimal? UnitPrice
    {
        get; set;
    }
    public string? UnitCode
    {
        get; set;
    }
    public string? BillingBasis
    {
        get; set;
    }

    public Invoices? Invoice
    {
        get; set;
    }
    public Service? Service
    {
        get; set;
    }
    public PropertyServiceRates? PropertyServiceRate
    {
        get; set;
    }
}
