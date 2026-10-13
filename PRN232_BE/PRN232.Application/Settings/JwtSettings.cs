namespace PRN232.Application.Settings;

// POCO thuần, không phụ thuộc Microsoft.Extensions.Configuration — bind ở Program.cs
// (PRN232.Api) rồi đăng ký làm singleton, Infrastructure chỉ nhận object này qua DI.
public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 120;
}
