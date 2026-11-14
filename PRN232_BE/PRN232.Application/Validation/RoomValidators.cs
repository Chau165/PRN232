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

    [GeneratedRegex(@"^[A-Za-z0-9.\-]+$")]
    private static partial Regex RoomCodePattern();

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
    }
}

public sealed class UpdateRoomDtoValidator : AbstractValidator<UpdateRoomDto>
{
    public UpdateRoomDtoValidator()
    {
        RuleFor(dto => dto.RoomTypeId).GreaterThan(0).WithMessage("Vui lòng chọn loại phòng.");
        RuleFor(dto => dto.RoomCode).ValidRoomCode();
        RuleFor(dto => dto.AreaSqm).ValidAreaSqm();
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
