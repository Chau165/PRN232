namespace PRN232.Application.Policies;

// Rebuild 3.2 (PLAN.md D3): booking đã cọc giữ phòng đến HẾT ngày "ngày dọn vào + 3 ngày ân hạn" theo giờ
// Việt Nam. Hết hạn mà chưa ký hợp đồng thì quản lý được nhả phòng và khách mất cọc.
// Booking tạo trước rebuild (không có ngày dọn vào) giữ luật cũ OD07: 7 ngày tính từ lúc cọc.
public static class DepositHoldPolicy
{
    public static readonly TimeSpan HoldPeriod = TimeSpan.FromDays(7);
    public const int MoveInGraceDays = 3;
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    // Null when the deposit time is unknown (legacy payments without PaidAtUtc).
    public static DateTime? HoldUntilUtc(DateTime? depositPaidAtUtc, DateOnly? moveInDate = null)
    {
        if (moveInDate is null)
        {
            return depositPaidAtUtc?.Add(HoldPeriod);
        }

        // Ví dụ: dọn vào 20/11 thì giữ hết 23/11, tức đến 00:00 ngày 24/11 giờ VN (= 17:00 ngày 23/11 UTC).
        var endLocal = moveInDate.Value.AddDays(MoveInGraceDays + 1).ToDateTime(TimeOnly.MinValue);
        return TimeZoneInfo.ConvertTimeToUtc(endLocal, BusinessTimeZone);
    }

    // Legacy deposits without a paid time were taken long ago, so their hold counts as over.
    public static bool IsHoldOver(DateTime? depositPaidAtUtc, DateTime nowUtc, DateOnly? moveInDate = null) =>
        HoldUntilUtc(depositPaidAtUtc, moveInDate) is not { } holdUntil || holdUntil <= nowUtc;
}
