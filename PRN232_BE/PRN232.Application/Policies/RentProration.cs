namespace PRN232.Application.Policies;

// Tiền phòng tháng đầu/tháng cuối khi hợp đồng bắt đầu hoặc kết thúc giữa tháng. Hóa đơn và chỉ số điện nước đều
// chốt theo tháng dương lịch, nên tháng không trọn được tính theo số ngày ở:
//   tiền phòng × số ngày ở trong tháng ÷ số ngày của tháng (làm tròn đến đồng).
// Ngày ở tính cả ngày bắt đầu lẫn ngày cuối (giống dịch vụ theo đầu người, C22). Ngày cuối là ngày kết thúc hợp
// đồng, hoặc ngày trả phòng đã báo nếu sớm hơn (C16): khách trả phòng sớm không trả tiền những ngày không ở — việc
// báo trước không đủ 30 ngày đã bị xử lý bằng cọc khi quyết toán.
// Ví dụ HĐ 15/10/2026–14/10/2027, 3.100.000đ/tháng: tháng 10/2026 = 17/31 ngày = 1.700.000đ; tháng 10/2027 =
// 14/31 ngày = 1.400.000đ; cộng lại đúng 12 tháng tiền phòng thay vì 13.
public static class RentProration
{
    public static DateOnly LastBilledDay(DateOnly endDate, DateOnly? plannedMoveOutDate) =>
        plannedMoveOutDate is { } planned && planned < endDate ? planned : endDate;

    public static (int Days, int DaysInMonth) BilledDays(DateOnly month, DateOnly startDate, DateOnly lastBilledDay)
    {
        var first = new DateOnly(month.Year, month.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var from = startDate > first ? startDate : first;
        var to = lastBilledDay < last ? lastBilledDay : last;
        return (Math.Max(0, to.DayNumber - from.DayNumber + 1), last.Day);
    }

    public static decimal Rent(decimal monthlyRent, int days, int daysInMonth) =>
        days >= daysInMonth
            ? monthlyRent
            : Math.Round(monthlyRent * days / daysInMonth, 0, MidpointRounding.AwayFromZero);
}
