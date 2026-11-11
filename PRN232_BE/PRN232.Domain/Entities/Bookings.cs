namespace PRN232.Domain.Entities;

public class Bookings
{
    public int BookingId
    {
        get; set;
    }
    public int RoomId
    {
        get; set;
    }
    public int CustomerId
    {
        get; set;
    }
    public decimal DepositAmount
    {
        get; set;
    }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiryDate
    {
        get; set;
    }

    public Rooms? Room
    {
        get; set;
    }
    public Users? Customer
    {
        get; set;
    }
    public Contracts? Contract
    {
        get; set;
    }
    public ICollection<Payments> Payments { get; set; } = new List<Payments>();
    public ICollection<PaymentRequests> PaymentRequests { get; set; } = new List<PaymentRequests>();
    public ICollection<BookingRefundRequests> RefundRequests { get; set; } = new List<BookingRefundRequests>();
}
