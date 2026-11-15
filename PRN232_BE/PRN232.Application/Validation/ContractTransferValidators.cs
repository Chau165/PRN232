using FluentValidation;
using PRN232.Application.DTOs;

namespace PRN232.Application.Validation;

// Rebuild 4.2: luật không cần DB. Ngày hiệu lực trong thời hạn hợp đồng, người nhận hợp lệ: ContractTransferService.
public sealed class CreateContractTransferDtoValidator : AbstractValidator<CreateContractTransferDto>
{
    public CreateContractTransferDtoValidator()
    {
        RuleFor(dto => dto.ContractId).GreaterThan(0).WithMessage("Vui lòng chọn hợp đồng.");
        RuleFor(dto => dto.NewHolderEmail)
            .NotEmpty().WithMessage("Vui lòng nhập email tài khoản của người nhận.")
            .EmailAddress().WithMessage("Email không hợp lệ.");
        RuleFor(dto => dto.EffectiveDate)
            .Must(date => date.Day == 1)
            .WithMessage("Ngày hiệu lực phải là ngày 1 của một tháng (điện nước và hóa đơn chốt theo tháng).");
    }
}

public sealed class CancelContractTransferDtoValidator : AbstractValidator<CancelContractTransferDto>
{
    public CancelContractTransferDtoValidator()
    {
        RuleFor(dto => dto.Reason)
            .Must(reason => reason is null || reason.Trim().Length <= 500)
            .WithMessage("Lý do hủy tối đa 500 ký tự.");
    }
}
