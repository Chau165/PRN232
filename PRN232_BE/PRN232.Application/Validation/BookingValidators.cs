using FluentValidation;
using PRN232.Application.DTOs;

namespace PRN232.Application.Validation;

// Rebuild 3.1 (PLAN.md D3 bước 2): khách chọn ngày dự kiến dọn vào và số người ở khi đặt cọc.
// Giới hạn theo MaxOccupants của từng phòng cần đọc DB nên nằm ở BookingService.
public static class BookingRules
{
    // Giữ phòng quá lâu thì chủ trọ mất khách khác; 14 ngày đủ để khách thu xếp chỗ cũ.
    public const int MoveInWindowDays = 14;
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static string OccupancyExceeded(string roomCode, int maxOccupants) =>
        $"Phòng {roomCode} chỉ được ở tối đa {maxOccupants} người.";

    // "Hôm nay" theo giờ Việt Nam, giống ContractService/InvoiceService.
    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BusinessTimeZone).DateTime);
}

public sealed class CreateBookingDtoValidator : AbstractValidator<CreateBookingDto>
{
    public CreateBookingDtoValidator(TimeProvider clock)
    {
        RuleFor(dto => dto.MoveInDate)
            .Must(date => date is null || date >= BookingRules.Today(clock))
            .WithMessage("Ngày dọn vào không được ở quá khứ.")
            .Must(date => date is null || date <= BookingRules.Today(clock).AddDays(BookingRules.MoveInWindowDays))
            .WithMessage($"Ngày dọn vào tối đa {BookingRules.MoveInWindowDays} ngày kể từ hôm nay.");
        RuleFor(dto => dto.OccupantCount)
            .Must(count => count is null or >= 1 and <= RoomRules.MaxOccupantsLimit)
            .WithMessage($"Số người ở phải từ 1 đến {RoomRules.MaxOccupantsLimit}.");
    }
}
