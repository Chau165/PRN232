namespace PRN232.Application.Settings;

// Rebuild 3.3 (PLAN.md D3 bước 4): điều khoản cọc ghi vào booking MỚI lúc khách đặt cọc.
// false (mặc định) = cọc là tiền đảm bảo, hoàn khi trả phòng; true = luật cũ, cọc trừ vào tiền phòng tháng đầu.
// Để là cấu hình để BE deploy được trước FE: đặt true cho tới khi trang đặt cọc hiện câu điều khoản mới,
// rồi đổi false (không cần deploy lại). Booking đã tạo giữ nguyên điều khoản đã ghi.
public sealed class BookingDepositSettings
{
    public bool AppliesToFirstMonth { get; set; }
}
