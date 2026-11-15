using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Validation;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild 3.1 (PLAN.md D3 bước 2): move-in date within 14 days, occupants within the room's limit.
public sealed class BookingMoveInTests
{
    // 2026-11-14 18:00 UTC is already 01:00 on 15/11 in Vietnam, so "today" is 15/11.
    private static readonly CreateBookingDtoValidator Validator =
        new(new FixedTimeProvider(new DateTimeOffset(2026, 11, 14, 18, 0, 0, TimeSpan.Zero)));

    private static string? Error(DateOnly? moveIn = null, int? occupants = null) =>
        Validator.Validate(new CreateBookingDto(1, MoveInDate: moveIn, OccupantCount: occupants))
            .Errors.SingleOrDefault()?.ErrorMessage;

    [Theory]
    [InlineData(15)]
    [InlineData(29)]
    public void Move_in_from_today_to_14_days_ahead_passes(int day) =>
        Assert.Null(Error(new DateOnly(2026, 11, day), 1));

    [Fact]
    public void Omitted_fields_pass() => Assert.Null(Error());

    [Fact]
    public void Move_in_uses_the_vietnam_date() =>
        Assert.Equal("Ngày dọn vào không được ở quá khứ.", Error(new DateOnly(2026, 11, 14)));

    [Fact]
    public void Move_in_cannot_be_more_than_14_days_ahead() =>
        Assert.Equal("Ngày dọn vào tối đa 14 ngày kể từ hôm nay.", Error(new DateOnly(2026, 11, 30)));

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Occupants_must_be_between_1_and_10(int occupants) =>
        Assert.Equal("Số người ở phải từ 1 đến 10.", Error(occupants: occupants));

    [Fact]
    public async Task Api_saves_and_returns_move_in_date_and_occupants()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var moveIn = VietnamToday().AddDays(3);

        using var response = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1, moveInDate = moveIn, occupantCount = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(moveIn.ToString("yyyy-MM-dd"), body.GetProperty("moveInDate").GetString());
        Assert.Equal(1, body.GetProperty("occupantCount").GetInt32());
        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.SingleAsync();
        Assert.Equal((moveIn, 1), (saved.MoveInDate, saved.OccupantCount));
    }

    [Fact]
    public async Task Api_reports_too_many_occupants_on_the_occupant_field()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");

        // Room A101 allows 1 person (MaxOccupants default).
        using var response = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1, occupantCount = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Phòng A101 chỉ được ở tối đa 1 người.",
            body.GetProperty("errors").GetProperty("occupantCount")[0].GetString());
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.AnyAsync());
    }

    [Fact]
    public async Task Api_rejects_a_move_in_date_too_far_ahead_before_creating_anything()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");

        using var response = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1, moveInDate = VietnamToday().AddDays(30) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("moveInDate", out _));
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.AnyAsync());
    }

    private static DateOnly VietnamToday() => BookingRules.Today(TimeProvider.System);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
