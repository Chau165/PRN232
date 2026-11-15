namespace PRN232.Application.Settings;

// Rebuild 3.2: nhóm quyết định hệ thống KHÔNG hoàn tiền qua tài khoản; khách liên hệ trực tiếp quản lý.
// Luồng "yêu cầu hoàn cọc" (BookingRefundService, payout payOS) được GIỮ NGUYÊN code nhưng khóa lại.
// Bật lại: BookingRefunds:RequestsEnabled = true (cần bật cả PayOSPayout để thật sự chuyển tiền).
public sealed class BookingRefundSettings
{
    public bool RequestsEnabled { get; set; }
}
