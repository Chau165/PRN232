using FluentValidation;
using PRN232.Application.DTOs;

namespace PRN232.Application.Validation;

// Rebuild 4.1: luật không cần DB. Sức chứa phòng và ngày trong thời hạn hợp đồng: ContractOccupantService.
public sealed class AddContractOccupantDtoValidator : AbstractValidator<AddContractOccupantDto>
{
    public AddContractOccupantDtoValidator()
    {
        RuleFor(dto => dto.FullName).ValidFullName();
        RuleFor(dto => dto.PhoneNumber!).ValidPhone();
        RuleFor(dto => dto.IdentityNumber).ValidCccd();
        RuleFor(dto => dto.Note)
            .Must(note => note is null || note.Trim().Length <= 500)
            .WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}
