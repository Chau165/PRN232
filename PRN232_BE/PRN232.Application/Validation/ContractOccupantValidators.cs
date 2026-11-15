using System.Text.RegularExpressions;
using FluentValidation;
using PRN232.Application.DTOs;

namespace PRN232.Application.Validation;

// Rebuild 4.1: luật không cần DB. Sức chứa phòng và ngày trong thời hạn hợp đồng: ContractOccupantService.
public sealed partial class AddContractOccupantDtoValidator : AbstractValidator<AddContractOccupantDto>
{
    public AddContractOccupantDtoValidator()
    {
        RuleFor(dto => dto.FullName).ValidFullName();
        RuleFor(dto => dto.PhoneNumber!).ValidPhone();
        RuleFor(dto => dto.IdentityNumber)
            .Must(value => value is not null && CccdPattern().IsMatch(value.Trim()))
            .WithMessage("Số CCCD phải gồm đúng 12 chữ số.");
        RuleFor(dto => dto.Note)
            .Must(note => note is null || note.Trim().Length <= 500)
            .WithMessage("Ghi chú tối đa 500 ký tự.");
    }

    [GeneratedRegex("^[0-9]{12}$")]
    private static partial Regex CccdPattern();
}
