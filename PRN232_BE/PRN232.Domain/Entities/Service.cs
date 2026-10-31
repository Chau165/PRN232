namespace PRN232.Domain.Entities;

public class Service
{
    public int ServiceId
    {
        get; set;
    }
    public string Name { get; set; } = string.Empty;
    public decimal Price
    {
        get; set;
    }

    public ICollection<RoomTypeServices> RoomTypeServices { get; set; } = new List<RoomTypeServices>();
    public ICollection<PropertyServiceRates> PropertyServiceRates { get; set; } = new List<PropertyServiceRates>();
    public ICollection<InvoiceDetails> InvoiceDetails { get; set; } = new List<InvoiceDetails>();
}
