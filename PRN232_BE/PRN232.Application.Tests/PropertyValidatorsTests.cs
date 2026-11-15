using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Api.Controllers;
using PRN232.Api.Validation;
using PRN232.Application.DTOs;
using PRN232.Application.Settings;
using PRN232.Application.Validation;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild 1.2b (plan D2, D5): property text limits, equipment dates, utility price ceilings.
public sealed class PropertyValidatorsTests
{
    private static readonly PropertyWriteFormValidator PropertyForm = new();
    private static readonly CreatePropertyServiceRateDtoValidator Rate = new(new UtilityPriceLimits());

    // 2026-11-15 03:00 UTC is 10:00 on 15/11 in Vietnam.
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero));
    private static readonly CreateEquipmentDtoValidator CreateEquipment = new(Clock);
    private static readonly UpdateEquipmentDtoValidator UpdateEquipment = new(Clock);

    private static string? PropertyError(string? name = "Nhà trọ Hoa Sen", string? address = "12 Nguyễn Văn Bảo",
        string? description = null) =>
        PropertyForm.Validate(new PropertyWriteForm { PropertyName = name, AddressText = address, Description = description })
            .Errors.FirstOrDefault()?.ErrorMessage;

    [Fact]
    public void Realistic_property_passes() =>
        Assert.Null(PropertyError(description: new string('a', 2000)));

    [Theory]
    [InlineData(null, "Tên khu trọ không được để trống.")]
    [InlineData("  ", "Tên khu trọ không được để trống.")]
    [InlineData("AB", "Tên khu trọ phải từ 3 đến 150 ký tự.")]
    public void Property_name_is_required_and_bounded(string? name, string expected)
    {
        Assert.Equal(expected, PropertyError(name: name));
        Assert.Equal("Tên khu trọ phải từ 3 đến 150 ký tự.", PropertyError(name: new string('a', 151)));
    }

    [Theory]
    [InlineData(null, "Số nhà, tên đường không được để trống.")]
    [InlineData("12", "Số nhà, tên đường phải từ 5 đến 255 ký tự.")]
    public void Street_address_is_required_and_bounded(string? address, string expected)
    {
        Assert.Equal(expected, PropertyError(address: address));
        Assert.Equal("Số nhà, tên đường phải từ 5 đến 255 ký tự.", PropertyError(address: new string('a', 256)));
    }

    [Fact]
    public void Description_is_at_most_2000_characters() =>
        Assert.Equal("Mô tả tối đa 2000 ký tự.", PropertyError(description: new string('a', 2001)));

    private static string? RateError(decimal price, string unitCode, string basis) =>
        Rate.Validate(new CreatePropertyServiceRateDto(1, price, unitCode, basis, new DateOnly(2026, 12, 1)))
            .Errors.FirstOrDefault()?.ErrorMessage;

    [Theory]
    [InlineData(1_901, "kwh", "usage")]
    [InlineData(4_152, "kwh", "usage")]
    [InlineData(16_560, "m3", "usage")]
    [InlineData(100_000, "person_month", "occupant")]
    [InlineData(1_000_000, "room_month", "fixed_room")]
    public void Realistic_rates_pass(decimal price, string unitCode, string basis) =>
        Assert.Null(RateError(price, unitCode, basis));

    [Fact]
    public void Electricity_cannot_exceed_the_top_evn_tier_with_vat() =>
        Assert.Equal("Giá điện tối đa 4.152đ/kWh (bậc cao nhất của EVN đã gồm VAT). Chủ trọ thu cao hơn hóa đơn điện có thể bị phạt.",
            RateError(4_153, "kwh", "usage"));

    [Fact]
    public void Water_cannot_exceed_the_configured_ceiling() =>
        Assert.Equal("Giá nước tối đa 16.560đ/m³ (bậc cao nhất của công ty cấp nước đã gồm VAT và phí môi trường).",
            RateError(16_561, "m3", "usage"));

    [Theory]
    [InlineData("person_month", "occupant")]
    [InlineData("vehicle_month", "vehicle")]
    [InlineData("room_month", "fixed_room")]
    public void Monthly_fees_are_capped(string unitCode, string basis) =>
        Assert.Equal("Phí dịch vụ hằng tháng tối đa 1.000.000đ.", RateError(1_000_001, unitCode, basis));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Price_must_be_positive(decimal price) =>
        Assert.Equal("Đơn giá phải lớn hơn 0.", RateError(price, "kwh", "usage"));

    [Fact]
    public void Price_is_whole_dong() =>
        Assert.Equal("Đơn giá phải là số đồng nguyên.", RateError(2_000.5m, "kwh", "usage"));

    [Fact]
    public void Ceilings_follow_configuration()
    {
        var after2026 = new CreatePropertyServiceRateDtoValidator(new UtilityPriceLimits { ElectricityMaxPricePerKwh = 4_229m });
        Assert.True(after2026.Validate(new CreatePropertyServiceRateDto(1, 4_229m, "kwh", "usage", new DateOnly(2027, 1, 1))).IsValid);
    }

    private static CreateEquipmentDto Equipment(DateOnly install, DateOnly warranty, string name = "Máy lạnh Daikin",
        string supplier = "Điện máy Xanh", int roomId = 1) =>
        new(roomId, name, install, warranty, supplier, "Operational");

    [Fact]
    public void Realistic_equipment_passes() =>
        Assert.True(CreateEquipment.Validate(Equipment(new DateOnly(2026, 11, 15), new DateOnly(2028, 11, 15))).IsValid);

    [Fact]
    public void Install_date_cannot_be_in_the_future() =>
        Assert.Equal("Ngày lắp đặt không được ở tương lai.",
            CreateEquipment.Validate(Equipment(new DateOnly(2026, 11, 16), new DateOnly(2028, 1, 1))).Errors.Single().ErrorMessage);

    [Fact]
    public void Equipment_rules_report_the_failing_field()
    {
        var errors = CreateEquipment.Validate(Equipment(new DateOnly(2026, 1, 1), new DateOnly(2025, 1, 1), "", "", 0))
            .Errors.Select(error => error.PropertyName);
        Assert.Equal(["RoomId", "Name", "Supplier", "WarrantyExpiry"], errors);

        var update = UpdateEquipment.Validate(new UpdateEquipmentDto("Tủ lạnh", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), "Nguyễn Kim", "Broken"));
        Assert.Equal("Trạng thái thiết bị không được hỗ trợ.", update.Errors.Single().ErrorMessage);
    }

    // Rebuild 2.2: quantity and the per-item compensation value written in the handover record.
    [Theory]
    [InlineData(null, null)]
    [InlineData(1, 0)]
    [InlineData(20, 50_000_000)]
    [InlineData(2, 8_500_000)]
    public void Realistic_quantity_and_value_pass(int? quantity, int? value) =>
        Assert.Empty(CreateEquipment.Validate(Equipment(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1))
            with { Quantity = quantity, CompensationValue = (decimal?)value }).Errors.Select(e => e.ErrorMessage));

    [Theory]
    [InlineData(0, 0, "Số lượng phải từ 1 đến 20.")]
    [InlineData(21, 0, "Số lượng phải từ 1 đến 20.")]
    [InlineData(1, -1_000, "Giá trị bồi thường phải từ 0đ đến 50.000.000đ.")]
    [InlineData(1, 50_001_000, "Giá trị bồi thường phải từ 0đ đến 50.000.000đ.")]
    [InlineData(1, 8_500_500, "Giá trị bồi thường phải là bội số của 1.000đ.")]
    public void Quantity_and_value_out_of_range_are_rejected(int quantity, double value, string expected)
    {
        var update = new UpdateEquipmentDto("Máy lạnh", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1),
            "Điện máy Xanh", "Operational", quantity, (decimal)value);
        Assert.Equal(expected, UpdateEquipment.Validate(update).Errors.Single().ErrorMessage);
    }

    [Fact]
    public void Validators_are_registered_for_the_api()
    {
        using var factory = new MaintenanceApiFactory();
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IValidator<PropertyWriteForm>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IValidator<CreatePropertyServiceRateDto>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IValidator<CreateEquipmentDto>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IValidator<UpdateEquipmentDto>>());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
