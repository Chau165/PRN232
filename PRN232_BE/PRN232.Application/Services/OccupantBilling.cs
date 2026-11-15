using PRN232.Application.ReadModels;

namespace PRN232.Application.Services;

// Rebuild 4.3 (PLAN.md D4/D5): dịch vụ theo đầu người. Số lượng = "người-tháng" của tháng hóa đơn:
//  - Người đứng tên tính theo số ngày hợp đồng có hiệu lực trong tháng (trọn tháng = 1; tháng đầu/cuối không trọn
//    thì chia theo ngày, giống tiền phòng — RentProration).
//  - Mỗi người ở cùng tính theo tỷ lệ số ngày ở trong tháng: MoveInDate <= ngày <= MoveOutDate, chia cho số ngày
//    của tháng. Tính CẢ ngày dọn ra, giống cách cả hệ thống hiểu ngày cuối (hợp đồng chạy đến hết EndDate; ngày dọn
//    ra không được sau EndDate; chuyển người đứng tên đóng người ở cùng ở HĐ cũ đúng ngày cuối HĐ cũ).
public static class OccupantBilling
{
    public static decimal PersonMonths(DateOnly month, OccupantStayReadModel holder, IEnumerable<OccupantStayReadModel> stays)
        => CalculatePersonMonths(month, stays.Prepend(holder));

    public static decimal AdditionalPersonMonths(DateOnly month, IEnumerable<OccupantStayReadModel> stays)
        => CalculatePersonMonths(month, stays);

    private static decimal CalculatePersonMonths(DateOnly month, IEnumerable<OccupantStayReadModel> stays)
    {
        var first = new DateOnly(month.Year, month.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var daysInMonth = last.Day;
        var total = 0m;
        foreach (var stay in stays)
        {
            var from = stay.MoveInDate > first ? stay.MoveInDate : first;
            var to = stay.MoveOutDate is { } moveOut && moveOut < last ? moveOut : last;
            var days = to.DayNumber - from.DayNumber + 1;
            if (days > 0)
            {
                total += (decimal)days / daysInMonth;
            }
        }
        // Làm tròn 2 chữ số để dòng hóa đơn "2,45 người × đơn giá" khớp với số tiền khách thấy.
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
}

