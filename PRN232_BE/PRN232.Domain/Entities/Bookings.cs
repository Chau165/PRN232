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
    public decimal? AgreedMonthlyRent { get; set; }
    public int? ViewingId { get; set; }
    // Rebuild 3.1 (PLAN D3 bước 2). NULL ở booking tạo trước rebuild.
    public DateOnly? MoveInDate { get; set; }
    public int? OccupantCount { get; set; }
    // Rebuild 3.2 (PLAN D3 bước 3): ai hủy quyết định cọc mất hay phải hoàn. NULL ở booking hủy trước rebuild.
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
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
    public ViewingSchedules? Viewing { get; set; }
    public ICollection<Payments> Payments { get; set; } = new List<Payments>();
    public ICollection<PaymentRequests> PaymentRequests { get; set; } = new List<PaymentRequests>();
    public ICollection<BookingRefundRequests> RefundRequests { get; set; } = new List<BookingRefundRequests>();
}
