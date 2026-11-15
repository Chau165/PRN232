using FluentValidation;
using PRN232.Application.DTOs;
using PRN232.Application.States;

namespace PRN232.Application.Validation;

// Rebuild 3.5: luật không cần đọc DB. Luật theo hợp đồng/thiết bị (đúng danh sách thiết bị của phòng,
// trần bồi thường, hỏng từ lúc nhận phòng) nằm ở HandoverService.
public sealed class SaveHandoverDtoValidator : AbstractValidator<SaveHandoverDto>
{
    public const int NoteMaxLength = 1000;
    public const int ItemNoteMaxLength = 500;

    public SaveHandoverDtoValidator(TimeProvider clock)
    {
        RuleFor(dto => dto.HandoverDate)
            .Must(date => date <= BookingRules.Today(clock))
            .WithMessage("Ngày bàn giao không được ở tương lai.");
        RuleFor(dto => dto.Note)
            .Must(note => note is null || note.Trim().Length <= NoteMaxLength)
            .WithMessage($"Ghi chú biên bản tối đa {NoteMaxLength} ký tự.");
        RuleFor(dto => dto.Items)
            .NotNull().WithMessage("Danh sách thiết bị là bắt buộc.")
            .Must(items => items is null || items.Select(item => item.EquipmentId).Distinct().Count() == items.Count)
            .WithMessage("Mỗi thiết bị chỉ được ghi một lần.");
        RuleForEach(dto => dto.Items).ChildRules(item =>
        {
            item.RuleFor(line => line.EquipmentId).GreaterThan(0).WithMessage("Thiết bị không hợp lệ.");
            item.RuleFor(line => line.Condition)
                .Must(EquipmentConditions.IsValid)
                .WithMessage("Tình trạng phải là Tốt, Hao mòn, Hư hỏng hoặc Mất.");
            item.RuleFor(line => line.ChargeAmount)
                .Must(amount => amount is null or >= 0m)
                .WithMessage("Tiền bồi thường không được âm.")
                .Must(amount => amount is null or < 0m || amount % EquipmentRules.CompensationValueStep == 0m)
                .WithMessage("Tiền bồi thường phải là bội số của 1.000đ.");
            item.RuleFor(line => line.Note)
                .Must(note => note is null || note.Trim().Length <= ItemNoteMaxLength)
                .WithMessage($"Ghi chú thiết bị tối đa {ItemNoteMaxLength} ký tự.");
        });
    }
}
