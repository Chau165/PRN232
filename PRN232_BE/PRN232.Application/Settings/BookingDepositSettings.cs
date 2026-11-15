namespace PRN232.Application.Settings;

// Điều khoản được chốt vào từng booking khi tạo. Booking cũ luôn giữ nguyên giá trị đã lưu.
public sealed class BookingDepositSettings
{
    public bool AppliesToFirstMonth { get; set; } = true;
}
