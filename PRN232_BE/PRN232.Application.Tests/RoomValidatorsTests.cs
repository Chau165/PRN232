using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Validation;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild 1.2 (plan D2): realistic bounds for rooms and property-scoped rent.
public sealed class RoomValidatorsTests
{
    private static readonly CreateRoomDtoValidator CreateRoom = new();
    private static readonly UpdateRoomDtoValidator UpdateRoom = new();
    private static readonly CreatePropertyRoomTypeDtoValidator CreateRent = new();
    private static readonly UpdatePropertyRoomTypeDtoValidator UpdateRent = new();

    private static string? RoomError(string roomCode = "A-101", decimal areaSqm = 24.5m, int propertyId = 1, int roomTypeId = 1) =>
        CreateRoom.Validate(new CreateRoomDto(propertyId, roomTypeId, roomCode, areaSqm)).Errors.FirstOrDefault()?.ErrorMessage;

    [Theory]
    [InlineData("A-101", 24.5)]
    [InlineData("101", 8)]
    [InlineData("B2.05", 100)]
    [InlineData("  P12  ", 15)]
    public void Realistic_room_passes(string roomCode, decimal areaSqm) =>
        Assert.Null(RoomError(roomCode, areaSqm));

    [Theory]
    [InlineData(7.9)]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.1)]
    [InlineData(99999)]
    public void Area_must_be_between_8_and_100_square_meters(decimal areaSqm) =>
        Assert.Equal("Diện tích phòng phải từ 8 đến 100 m².", RoomError(areaSqm: areaSqm));

    [Fact]
    public void Area_has_at_most_one_decimal_place() =>
        Assert.Equal("Diện tích chỉ được có tối đa 1 chữ số thập phân.", RoomError(areaSqm: 20.25m));

    [Theory]
    [InlineData("", "Mã phòng không được để trống.")]
    [InlineData("   ", "Mã phòng không được để trống.")]
    [InlineData("Phòng 101", "Mã phòng chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc dấu chấm (ví dụ: A-101).")]
    [InlineData("A_101", "Mã phòng chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc dấu chấm (ví dụ: A-101).")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU", "Mã phòng tối đa 20 ký tự.")]
    public void Room_code_is_short_and_plain(string roomCode, string expected) =>
        Assert.Equal(expected, RoomError(roomCode: roomCode));

    [Fact]
    public void Property_and_room_type_must_be_chosen()
    {
        Assert.Equal("Vui lòng chọn khu trọ.", RoomError(propertyId: 0));
        Assert.Equal("Vui lòng chọn loại phòng.", RoomError(roomTypeId: 0));
    }

    [Fact]
    public void Update_uses_the_same_room_rules()
    {
        var errors = UpdateRoom.Validate(new UpdateRoomDto(0, "Phòng 1", 500m)).Errors.Select(e => e.PropertyName);
        Assert.Equal(["RoomTypeId", "RoomCode", "AreaSqm"], errors);
    }

    [Theory]
    [InlineData(500_000)]
    [InlineData(3_500_000)]
    [InlineData(50_000_000)]
    public void Realistic_rent_passes(decimal rent)
    {
        Assert.True(CreateRent.Validate(new CreatePropertyRoomTypeDto(1, rent)).IsValid);
        Assert.True(UpdateRent.Validate(new UpdatePropertyRoomTypeDto(rent)).IsValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3_000)]
    [InlineData(499_000)]
    [InlineData(50_001_000)]
    public void Rent_must_be_between_500_thousand_and_50_million(decimal rent) =>
        Assert.Equal("Giá thuê phải từ 500.000đ đến 50.000.000đ mỗi tháng.",
            UpdateRent.Validate(new UpdatePropertyRoomTypeDto(rent)).Errors.Single().ErrorMessage);

    [Fact]
    public void Rent_is_a_whole_thousand() =>
        Assert.Equal("Giá thuê phải là bội số của 1.000đ.",
            CreateRent.Validate(new CreatePropertyRoomTypeDto(1, 2_500_500m)).Errors.Single().ErrorMessage);

    [Fact]
    public void Validators_are_registered_for_the_api()
    {
        using var factory = new MaintenanceApiFactory();
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IValidator<CreateRoomDto>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IValidator<UpdateRoomDto>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IValidator<CreatePropertyRoomTypeDto>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IValidator<UpdatePropertyRoomTypeDto>>());
    }
}
