using FluentValidation;
using PRN232.Application.DTOs;
using PRN232.Application.States;

namespace PRN232.Application.Validation;

// Rebuild 1.2b: cùng quy tắc EquipmentService đang kiểm tra (giữ nguyên câu báo lỗi), thêm
// "ngày lắp đặt không ở tương lai", và báo lỗi theo từng field thay vì một câu duy nhất.
public static class EquipmentRules
{
    public const int TextMaxLength = 200;
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static IRuleBuilderOptions<T, string> ValidEquipmentText<T>(this IRuleBuilder<T, string> rule, string label) =>
        rule.Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= TextMaxLength)
            .WithMessage($"{label} là bắt buộc và không quá {TextMaxLength} ký tự.");

    public static IRuleBuilderOptions<T, DateOnly> NotInFuture<T>(this IRuleBuilder<T, DateOnly> rule, TimeProvider clock) =>
        rule.Must(value => value <= Today(clock)).WithMessage("Ngày lắp đặt không được ở tương lai.");

    public static IRuleBuilderOptions<T, string> ValidEquipmentStatus<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(value => value is not null && EquipmentStatuses.IsValid(value.Trim()))
            .WithMessage("Trạng thái thiết bị không được hỗ trợ.");

    // Rebuild 2.2 (PLAN.md D6). Giá thiết bị phổ biến trong phòng trọ năm 2026: máy lạnh 1 HP khoảng
    // 6–9 triệu, tủ lạnh 5–6 triệu, tivi 43 inch 4–13 triệu, máy giặt khoảng 11 triệu. Trần 50 triệu
    // mỗi cái đủ cho đồ cao cấp mà vẫn chặn được lỗi gõ thừa số 0.
    public const int QuantityMax = 20;
    public const decimal CompensationValueMax = 50_000_000m;
    public const decimal CompensationValueStep = 1_000m;

    public static IRuleBuilderOptions<T, int?> ValidQuantity<T>(this IRuleBuilder<T, int?> rule) =>
        rule.Must(value => value is null or >= 1 and <= QuantityMax)
            .WithMessage($"Số lượng phải từ 1 đến {QuantityMax}.");

    // 0đ hợp lệ: đồ giá trị nhỏ chủ trọ không tính bồi thường (rèm, móc treo...).
    public static IRuleBuilderOptions<T, decimal?> ValidCompensationValue<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule
            .Must(value => value is null or >= 0m and <= CompensationValueMax)
            .WithMessage("Giá trị bồi thường phải từ 0đ đến 50.000.000đ.")
            .Must(value => value is null or < 0m or > CompensationValueMax || value % CompensationValueStep == 0m)
            .WithMessage("Giá trị bồi thường phải là bội số của 1.000đ.");

    // "Hôm nay" theo giờ Việt Nam, giống ContractService/InvoiceService.
    private static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BusinessTimeZone).DateTime);
}

public sealed class CreateEquipmentDtoValidator : AbstractValidator<CreateEquipmentDto>
{
    public CreateEquipmentDtoValidator(TimeProvider clock)
    {
        RuleFor(dto => dto.RoomId).GreaterThan(0).WithMessage("Vui lòng chọn phòng.");
        RuleFor(dto => dto.Name).ValidEquipmentText("Tên thiết bị");
        RuleFor(dto => dto.Supplier).ValidEquipmentText("Nhà cung cấp");
        RuleFor(dto => dto.InstallDate).NotInFuture(clock);
        RuleFor(dto => dto.WarrantyExpiry).GreaterThanOrEqualTo(dto => dto.InstallDate)
            .WithMessage("Hạn bảo hành không được trước ngày lắp đặt.");
        RuleFor(dto => dto.Status).ValidEquipmentStatus();
        RuleFor(dto => dto.Quantity).ValidQuantity();
        RuleFor(dto => dto.CompensationValue).ValidCompensationValue();
    }
}

public sealed class UpdateEquipmentDtoValidator : AbstractValidator<UpdateEquipmentDto>
{
    public UpdateEquipmentDtoValidator(TimeProvider clock)
    {
        RuleFor(dto => dto.Name).ValidEquipmentText("Tên thiết bị");
        RuleFor(dto => dto.Supplier).ValidEquipmentText("Nhà cung cấp");
        RuleFor(dto => dto.InstallDate).NotInFuture(clock);
        RuleFor(dto => dto.WarrantyExpiry).GreaterThanOrEqualTo(dto => dto.InstallDate)
            .WithMessage("Hạn bảo hành không được trước ngày lắp đặt.");
        RuleFor(dto => dto.Status).ValidEquipmentStatus();
        RuleFor(dto => dto.Quantity).ValidQuantity();
        RuleFor(dto => dto.CompensationValue).ValidCompensationValue();
    }
}
