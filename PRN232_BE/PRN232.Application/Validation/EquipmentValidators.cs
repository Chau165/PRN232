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
    }
}
