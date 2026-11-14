using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using PRN232.Application.DTOs;

namespace PRN232.Application.Validation;

// Rebuild 1.2 (docs/rebuild/PLAN.md, D2): giới hạn thực tế cho phòng và giá thuê theo khu trọ.
// Các con số là đề xuất của plan; nhóm chốt số khác thì chỉ sửa hằng số ở đây.
// FE (RoomFormModal, trang giá theo khu trọ) nên dùng cùng giới hạn và câu báo lỗi.
public static partial class RoomRules
{
    public const decimal AreaMinSqm = 8m;
    public const decimal AreaMaxSqm = 100m;
    public const int RoomCodeMaxLength = 20;
    public const decimal MonthlyRentMin = 500_000m;
    public const decimal MonthlyRentMax = 50_000_000m;
    public const decimal MonthlyRentStep = 1_000m;

    // Rebuild 2.1: Luật Nhà ở 2023 + NĐ 100/2024 + TT 05/2024 yêu cầu dãy nhà trọ có diện tích bình
    // quân tối thiểu 8 m²/người (không tính khu phụ); Luật Cư trú 2020 cũng lấy 8 m² sàn/người làm mức
    // sàn đăng ký thường trú. Hệ thống áp mức này cho mọi phòng (thận trọng). Xem PLAN.md D2.
    public const decimal AreaPerOccupantSqm = 8m;
    public const int MaxOccupantsLimit = 10;
    public const int FloorMax = 50;
    public const int RoomDescriptionMaxLength = 1000;

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    // Số người tối đa cho phép theo diện tích: 20 m² -> 2, 7 m² (phòng cũ) -> 1, 200 m² -> 10.
    public static int MaxOccupantsFor(decimal areaSqm) =>
        (int)Math.Clamp(Math.Floor(areaSqm / AreaPerOccupantSqm), 1m, MaxOccupantsLimit);

    [GeneratedRegex(@"^[A-Za-z0-9.\-]+$")]
    private static partial Regex RoomCodePattern();

    // Chỉ kiểm tra khi client gửi giá trị. Bỏ qua phép so với diện tích khi diện tích tự nó đã sai,
    // để người dùng chỉ thấy lỗi diện tích.
    public static void ValidMaxOccupants<T>(this IRuleBuilder<T, int?> rule, Func<T, decimal> areaSqm)
    {
        rule
            .Must(value => value is null or >= 1 and <= MaxOccupantsLimit)
            .WithMessage($"Số người tối đa phải từ 1 đến {MaxOccupantsLimit}.")
            .Must((dto, value) =>
            {
                var area = areaSqm(dto);
                return value is null or < 1 or > MaxOccupantsLimit ||
                       area < AreaMinSqm || area > AreaMaxSqm ||
                       value <= MaxOccupantsFor(area);
            })
            .WithMessage((dto, _) =>
            {
                var area = areaSqm(dto);
                return $"Phòng {area.ToString("0.#", Vietnamese)} m² chỉ được ở tối đa {MaxOccupantsFor(area)} người " +
                       $"(tối thiểu {AreaPerOccupantSqm:0} m²/người).";
            });
    }

    public static IRuleBuilderOptions<T, int?> ValidFloor<T>(this IRuleBuilder<T, int?> rule) =>
        rule.Must(value => value is null or >= 0 and <= FloorMax)
            .WithMessage($"Tầng phải từ 0 (tầng trệt) đến {FloorMax}.");

    public static IRuleBuilderOptions<T, string?> ValidRoomDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => value is null || value.Trim().Length <= RoomDescriptionMaxLength)
            .WithMessage($"Mô tả phòng tối đa {RoomDescriptionMaxLength} ký tự.");

    public static IRuleBuilderOptions<T, string> ValidRoomCode<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Mã phòng không được để trống.")
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= RoomCodeMaxLength)
            .WithMessage($"Mã phòng tối đa {RoomCodeMaxLength} ký tự.")
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > RoomCodeMaxLength ||
                           RoomCodePattern().IsMatch(value.Trim()))
            .WithMessage("Mã phòng chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc dấu chấm (ví dụ: A-101).");

    public static IRuleBuilderOptions<T, decimal> ValidAreaSqm<T>(this IRuleBuilder<T, decimal> rule) =>
        rule
            .InclusiveBetween(AreaMinSqm, AreaMaxSqm)
            .WithMessage($"Diện tích phòng phải từ {AreaMinSqm:0} đến {AreaMaxSqm:0} m².")
            .Must(value => value < AreaMinSqm || value > AreaMaxSqm || decimal.Round(value, 1) == value)
            .WithMessage("Diện tích chỉ được có tối đa 1 chữ số thập phân.");

    public static IRuleBuilderOptions<T, decimal> ValidMonthlyRent<T>(this IRuleBuilder<T, decimal> rule) =>
        rule
            .InclusiveBetween(MonthlyRentMin, MonthlyRentMax)
            .WithMessage("Giá thuê phải từ 500.000đ đến 50.000.000đ mỗi tháng.")
            .Must(value => value < MonthlyRentMin || value > MonthlyRentMax || value % MonthlyRentStep == 0m)
            .WithMessage("Giá thuê phải là bội số của 1.000đ.");
}

public sealed class CreateRoomDtoValidator : AbstractValidator<CreateRoomDto>
{
    public CreateRoomDtoValidator()
    {
        RuleFor(dto => dto.PropertyId).GreaterThan(0).WithMessage("Vui lòng chọn khu trọ.");
        RuleFor(dto => dto.RoomTypeId).GreaterThan(0).WithMessage("Vui lòng chọn loại phòng.");
        RuleFor(dto => dto.RoomCode).ValidRoomCode();
        RuleFor(dto => dto.AreaSqm).ValidAreaSqm();
        RuleFor(dto => dto.MaxOccupants).ValidMaxOccupants(dto => dto.AreaSqm);
        RuleFor(dto => dto.Floor).ValidFloor();
        RuleFor(dto => dto.Description).ValidRoomDescription();
    }
}

public sealed class UpdateRoomDtoValidator : AbstractValidator<UpdateRoomDto>
{
    public UpdateRoomDtoValidator()
    {
        RuleFor(dto => dto.RoomTypeId).GreaterThan(0).WithMessage("Vui lòng chọn loại phòng.");
        RuleFor(dto => dto.RoomCode).ValidRoomCode();
        RuleFor(dto => dto.AreaSqm).ValidAreaSqm();
        RuleFor(dto => dto.MaxOccupants).ValidMaxOccupants(dto => dto.AreaSqm);
        RuleFor(dto => dto.Floor).ValidFloor();
        RuleFor(dto => dto.Description).ValidRoomDescription();
    }
}

public sealed class CreatePropertyRoomTypeDtoValidator : AbstractValidator<CreatePropertyRoomTypeDto>
{
    public CreatePropertyRoomTypeDtoValidator()
    {
        RuleFor(dto => dto.RoomTypeId).GreaterThan(0).WithMessage("Vui lòng chọn loại phòng.");
        RuleFor(dto => dto.MonthlyRent).ValidMonthlyRent();
    }
}

public sealed class UpdatePropertyRoomTypeDtoValidator : AbstractValidator<UpdatePropertyRoomTypeDto>
{
    public UpdatePropertyRoomTypeDtoValidator()
    {
        RuleFor(dto => dto.MonthlyRent).ValidMonthlyRent();
    }
}
