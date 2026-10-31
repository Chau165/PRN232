namespace PRN232.Domain.Entities;

public class PropertyServiceRates
{
    public int RateId
    {
        get; set;
    }
    public int PropertyId
    {
        get; set;
    }
    public int ServiceId
    {
        get; set;
    }
    public decimal Price
    {
        get; set;
    }
    public string UnitCode { get; set; } = string.Empty;
    public string BillingBasis { get; set; } = string.Empty;
    public DateOnly EffectiveFrom
    {
        get; set;
    }
    public DateOnly? EffectiveTo
    {
        get; set;
    }

    public Property? Property
    {
        get; set;
    }
    public Service? Service
    {
        get; set;
    }
    public ICollection<InvoiceDetails> InvoiceDetails { get; set; } = new List<InvoiceDetails>();
}
