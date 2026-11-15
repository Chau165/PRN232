using System.Globalization;
using FluentValidation;
using PRN232.Application.DTOs;
using PRN232.Application.Settings;

namespace PRN232.Application.Validation;

// Rebuild 1.2b (docs/rebuild/PLAN.md, D2): giới hạn chữ cho khu trọ. Form khu trọ nằm ở tầng Api
// (multipart), nên validator của form ở PRN232.Api/Validation dùng lại các quy tắc này.
public static class PropertyRules
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 150;
    public const int AddressMinLength = 5;
    public const int AddressMaxLength = 255;
    public const int DescriptionMaxLength = 2000;

    public static IRuleBuilderOptions<T, string?> ValidPropertyName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.RequiredText("Tên khu trọ", NameMinLength, NameMaxLength);

    public static IRuleBuilderOptions<T, string?> ValidStreetAddress<T>(this IRuleBuilder<T, string?> rule) =>
        rule.RequiredText("Số nhà, tên đường", AddressMinLength, AddressMaxLength);

    public static IRuleBuilderOptions<T, string?> ValidDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => value is null || value.Trim().Length <= DescriptionMaxLength)
            .WithMessage($"Mô tả tối đa {DescriptionMaxLength} ký tự.");

    private static IRuleBuilderOptions<T, string?> RequiredText<T>(
        this IRuleBuilder<T, string?> rule, string label, int min, int max) =>
        rule
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage($"{label} không được để trống.")
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length >= min && value.Trim().Length <= max)
            .WithMessage($"{label} phải từ {min} đến {max} ký tự.");
}

// Đơn giá dịch vụ của khu trọ (điện, nước, phí hằng tháng). Cặp unitCode/billingBasis hợp lệ và
// ngày hiệu lực vẫn do PropertyService kiểm tra như cũ.
public sealed class CreatePropertyServiceRateDtoValidator : AbstractValidator<CreatePropertyServiceRateDto>
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public CreatePropertyServiceRateDtoValidator(UtilityPriceLimits limits)
    {
        RuleFor(dto => dto.ServiceId).GreaterThan(0).WithMessage("Vui lòng chọn dịch vụ.");
        RuleFor(dto => dto.Price)
            .GreaterThan(0m).WithMessage("Đơn giá phải lớn hơn 0.")
            .Must(price => price <= 0m || decimal.Round(price, 0) == price)
            .WithMessage("Đơn giá phải là số đồng nguyên.");

        RuleFor(dto => dto.Price)
            .LessThanOrEqualTo(limits.ElectricityMaxPricePerKwh)
            .WithMessage($"Giá điện tối đa {Money(limits.ElectricityMaxPricePerKwh)}đ/kWh (bậc cao nhất của EVN đã gồm VAT). Chủ trọ thu cao hơn hóa đơn điện có thể bị phạt.")
            .When(dto => dto.UnitCode == "kwh");
        RuleFor(dto => dto.Price)
            .LessThanOrEqualTo(limits.WaterMaxPricePerM3)
            .WithMessage($"Giá nước tối đa {Money(limits.WaterMaxPricePerM3)}đ/m³ (bậc cao nhất của công ty cấp nước đã gồm VAT và phí môi trường).")
            .When(dto => dto.UnitCode == "m3");
        RuleFor(dto => dto.Price)
            .LessThanOrEqualTo(limits.MonthlyFeeMax)
            .WithMessage($"Phí dịch vụ hằng tháng tối đa {Money(limits.MonthlyFeeMax)}đ.")
            .When(dto => dto.UnitCode is "room_month" or "person_month" or "vehicle_month");
    }

    // 4152 -> "4.152" (dấu chấm ngăn cách hàng nghìn như cách viết tiền ở Việt Nam).
    private static string Money(decimal value) => value.ToString("#,0", Vietnamese);
}
