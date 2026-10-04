namespace PRN232_BE.Models;

public class Payments
{
    public int PaymentId { get; set; }
    public int UserId { get; set; }
    public int? BookingId { get; set; }
    public int? ContractId { get; set; }
    public int? InvoiceId { get; set; }
    public int? ReadingId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public Users? User { get; set; }
    public Bookings? Booking { get; set; }
    public Contracts? Contract { get; set; }
    public Invoices? Invoice { get; set; }
    public UtilityReadings? Reading { get; set; }
}
