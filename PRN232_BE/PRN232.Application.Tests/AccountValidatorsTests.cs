using System.Net;
using System.Net.Http.Json;
using PRN232.Application.DTOs;
using PRN232.Application.Validation;
using Xunit;

namespace PRN232.Application.Tests;

// A10: quy tắc dữ liệu tài khoản. Câu báo lỗi phải khớp với PRN232_FE/src/utils/accountValidation.ts.
public sealed class AccountValidatorsTests
{
    private static string? RegisterError(string fullName = "Nguyễn Văn A", string email = "a@example.test",
        string password = "matkhau123", string phone = "0901234567") =>
        AccountValidation.FirstError(AccountValidation.Register, new RegisterDto(fullName, email, password, phone));

    [Fact]
    public void Valid_registration_passes()
    {
        Assert.Null(RegisterError());
        Assert.Null(RegisterError(phone: ""));
        Assert.Null(RegisterError(phone: "+84 901 234 567"));
        Assert.Null(RegisterError(phone: "090.123.4567"));
    }

    [Theory]
    [InlineData("", "Họ tên không được để trống.")]
    [InlineData("   ", "Họ tên không được để trống.")]
    [InlineData("A", "Họ tên phải từ 2 đến 100 ký tự.")]
    public void Full_name_is_required_and_bounded(string fullName, string expected)
    {
        Assert.Equal(expected, RegisterError(fullName: fullName));
        Assert.Equal("Họ tên phải từ 2 đến 100 ký tự.", RegisterError(fullName: new string('a', 101)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("a@b")]
    [InlineData("a b@example.test")]
    public void Email_must_look_like_an_address(string email)
    {
        Assert.Equal("Email không hợp lệ.", RegisterError(email: email));
    }

    [Theory]
    [InlineData("", "Mật khẩu phải có ít nhất 8 ký tự.")]
    [InlineData("abc123", "Mật khẩu phải có ít nhất 8 ký tự.")]
    [InlineData("chicochu", "Mật khẩu phải có cả chữ và số.")]
    [InlineData("12345678", "Mật khẩu phải có cả chữ và số.")]
    public void Password_needs_eight_characters_with_letters_and_digits(string password, string expected)
    {
        Assert.Equal(expected, RegisterError(password: password));
    }

    [Fact]
    public void Password_longer_than_bcrypt_limit_is_rejected()
    {
        Assert.Equal("Mật khẩu quá dài (tối đa 72 ký tự không dấu).", RegisterError(password: "a1" + new string('x', 71)));
        Assert.Null(RegisterError(password: "a1" + new string('x', 70)));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("090123456")]
    [InlineData("1901234567")]
    [InlineData("+8490123456")]
    public void Phone_must_be_a_vietnamese_number_when_given(string phone)
    {
        Assert.Equal("Số điện thoại không hợp lệ (ví dụ: 0901234567).", RegisterError(phone: phone));
    }

    [Fact]
    public void New_password_must_follow_rules_and_differ_from_current()
    {
        Assert.Equal("Mật khẩu mới phải có ít nhất 8 ký tự.",
            AccountValidation.FirstError(AccountValidation.ChangePassword, new ChangePasswordDto("old-pass-1", "short1")));
        Assert.Equal("Mật khẩu mới phải khác mật khẩu hiện tại.",
            AccountValidation.FirstError(AccountValidation.ChangePassword, new ChangePasswordDto("same-pass-1", "same-pass-1")));
        Assert.Null(AccountValidation.FirstError(AccountValidation.ChangePassword, new ChangePasswordDto("old-pass-1", "new-pass-2")));
    }

    [Fact]
    public void Profile_update_checks_name_and_phone()
    {
        Assert.Equal("Số điện thoại không hợp lệ (ví dụ: 0901234567).",
            AccountValidation.FirstError(AccountValidation.UpdateProfile, new UpdateProfileDto("Nguyễn Văn A", "123")));
        Assert.Null(AccountValidation.FirstError(AccountValidation.UpdateProfile, new UpdateProfileDto("Nguyễn Văn A", "")));
    }

    [Fact]
    public async Task Register_api_returns_400_with_the_rule_message()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Khách mới",
            email = "weak@example.test",
            password = "abc123",
            phoneNumber = "0901234567"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Mật khẩu phải có ít nhất 8 ký tự.", problem.GetProperty("detail").GetString());
    }
}
