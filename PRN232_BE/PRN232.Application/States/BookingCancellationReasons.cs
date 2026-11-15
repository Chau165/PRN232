namespace PRN232.Application.States;

// Rebuild 3.2 (PLAN.md D3 bước 3, Điều 328 BLDS 2015). Hệ thống không chuyển tiền hoàn: khoản phải hoàn
// do quản lý trả trực tiếp cho khách ngoài hệ thống.
public static class BookingCancellationReasons
{
    // Khách tự hủy: mất cọc (nếu đã cọc).
    public const string CustomerCancelled = "CustomerCancelled";
    // Chủ trọ/quản lý hủy khi còn trong thời hạn giữ phòng: hoàn 100% cọc.
    public const string LandlordCancelled = "LandlordCancelled";
    // Quản lý nhả phòng vì khách không đến nhận phòng hết thời hạn giữ phòng: mất cọc.
    public const string NoShow = "NoShow";
}
