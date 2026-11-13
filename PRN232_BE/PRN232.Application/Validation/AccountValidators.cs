using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using PRN232.Application.DTOs;

namespace PRN232.Application.Validation;

// Quy tắc dữ liệu tài khoản (A10), dùng chung cho đăng ký, Admin tạo user, sửa hồ sơ và đổi
// mật khẩu. Frontend (PRN232_FE/src/utils/accountValidation.ts) dùng đúng các quy tắc và câu
// báo lỗi này: sửa một bên thì sửa cả bên kia.
// Chỉ áp dụng khi tạo mới hoặc đổi: tài khoản cũ có mật khẩu ngắn vẫn đăng nhập bình thường.
public static partial class AccountRules
{
    public const int FullNameMinLength = 2;
    public const int FullNameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PasswordMinLength = 8;
    // BCrypt chỉ dùng 72 byte đầu của mật khẩu, phần dài hơn bị bỏ qua khi so khớp.
    public const int PasswordMaxBytes = 72;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^(0|\+84)\d{9}$")]
    private static partial Regex VietnamPhonePattern();

    [GeneratedRegex(@"[\s.\-()]")]
    private static partial Regex PhoneSeparators();

    [GeneratedRegex(@"\p{L}")]
    private static partial Regex Letter();

    [GeneratedRegex(@"\d")]
    private static partial Regex Digit();

    public static string NormalizePhone(string? phone) => PhoneSeparators().Replace(phone ?? string.Empty, string.Empty);

    public static bool IsValidEmail(string? email)
    {
        var trimmed = email?.Trim() ?? string.Empty;
        return trimmed.Length is > 0 and <= EmailMaxLength && EmailPattern().IsMatch(trimmed);
    }

    public static bool IsValidPhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) || VietnamPhonePattern().IsMatch(NormalizePhone(phone));

    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Họ tên không được để trống.")
            .Must(value => string.IsNullOrWhiteSpace(value) ||
                           value.Trim().Length is >= FullNameMinLength and <= FullNameMaxLength)
            .WithMessage($"Họ tên phải từ {FullNameMinLength} đến {FullNameMaxLength} ký tự.");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(IsValidEmail).WithMessage("Email không hợp lệ.");

    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(IsValidPhone).WithMessage("Số điện thoại không hợp lệ (ví dụ: 0901234567).");

    // label: "Mật khẩu" hoặc "Mật khẩu mới".
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule, string label) =>
        rule
            .Must(value => !string.IsNullOrEmpty(value) && value.Length >= PasswordMinLength)
            .WithMessage($"{label} phải có ít nhất {PasswordMinLength} ký tự.")
            .Must(value => string.IsNullOrEmpty(value) || Encoding.UTF8.GetByteCount(value) <= PasswordMaxBytes)
            .WithMessage($"{label} quá dài (tối đa {PasswordMaxBytes} ký tự không dấu).")
            .Must(value => string.IsNullOrEmpty(value) || (Letter().IsMatch(value) && Digit().IsMatch(value)))
            .WithMessage($"{label} phải có cả chữ và số.");
}

public sealed class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(dto => dto.FullName).ValidFullName();
        RuleFor(dto => dto.Email).ValidEmail();
        RuleFor(dto => dto.Password).ValidPassword("Mật khẩu");
        RuleFor(dto => dto.PhoneNumber).ValidPhone();
    }
}

public sealed class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(dto => dto.FullName).ValidFullName();
        RuleFor(dto => dto.Email).ValidEmail();
        RuleFor(dto => dto.Password).ValidPassword("Mật khẩu");
        RuleFor(dto => dto.PhoneNumber).ValidPhone();
    }
}

public sealed class UpdateProfileDtoValidator : AbstractValidator<UpdateProfileDto>
{
    public UpdateProfileDtoValidator()
    {
        RuleFor(dto => dto.FullName).ValidFullName();
        RuleFor(dto => dto.PhoneNumber).ValidPhone();
    }
}

public sealed class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(dto => dto.NewPassword).ValidPassword("Mật khẩu mới");
        RuleFor(dto => dto.NewPassword)
            .Must((dto, newPassword) => newPassword != dto.CurrentPassword)
            .WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
    }
}

public static class AccountValidation
{
    public static readonly RegisterDtoValidator Register = new();
    public static readonly CreateUserDtoValidator CreateUser = new();
    public static readonly UpdateProfileDtoValidator UpdateProfile = new();
    public static readonly ChangePasswordDtoValidator ChangePassword = new();

    // Trả về câu báo lỗi đầu tiên (theo thứ tự khai báo rule) hoặc null nếu hợp lệ.
    public static string? FirstError<T>(IValidator<T> validator, T dto)
    {
        var result = validator.Validate(dto);
        return result.IsValid ? null : result.Errors[0].ErrorMessage;
    }
}
