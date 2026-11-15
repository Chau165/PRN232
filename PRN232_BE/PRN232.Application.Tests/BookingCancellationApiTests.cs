using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Api.Controllers;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild 3.2 (PLAN.md D3 bước 3): the system never refunds through an account. A landlord cancellation
// shows the customer who to contact; the payOS refund-request flow is kept in code but locked.
public sealed class BookingCancellationApiTests
{
    [Fact]
    public async Task Refund_requests_are_locked_by_default()
    {
        using var factory = new BookingsApiFactory { RefundRequestsEnabled = false };
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var bookingId = await CreatePaidBookingAsync(client);

        using var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/refund-requests", new
        {
            reason = "Đổi ý",
            recipientBankBin = "970436",
            recipientAccountNumber = "0123456789",
            recipientAccountName = "NGUYEN VAN A"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(BookingRefundRequestsController.RequestsDisabledMessage, problem.GetProperty("detail").GetString());
        var booking = await client.GetFromJsonAsync<JsonElement>($"/api/bookings/{bookingId}");
        Assert.False(booking.GetProperty("canRequestRefund").GetBoolean());
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().BookingRefundRequests.AnyAsync());
    }

    [Fact]
    public async Task Landlord_cancellation_during_hold_owes_a_full_refund_and_shows_who_to_contact()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var bookingId = await CreatePaidBookingAsync(client);

        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        using var response = await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var booking = await client.GetFromJsonAsync<JsonElement>($"/api/bookings/{bookingId}");
        Assert.Equal("Cancelled", booking.GetProperty("status").GetString());
        Assert.Equal("LandlordCancelled", booking.GetProperty("cancellationReason").GetString());
        Assert.True(booking.GetProperty("depositRefundDue").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, booking.GetProperty("cancelledAtUtc").ValueKind);
        var contact = Assert.Single(booking.GetProperty("contacts").EnumerateArray());
        Assert.Equal(("Manager A", "0911000000", "Manager"),
            (contact.GetProperty("fullName").GetString(), contact.GetProperty("phoneNumber").GetString(), contact.GetProperty("role").GetString()));
    }

    [Fact]
    public async Task Customer_can_cancel_after_paying_and_forfeits_the_deposit()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var bookingId = await CreatePaidBookingAsync(client);

        using var response = await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CustomerCancelled", booking.GetProperty("cancellationReason").GetString());
        Assert.False(booking.GetProperty("depositRefundDue").GetBoolean());
        // Rebuild 3.2c: the assigned manager learns the room is free again.
        using var scope = factory.Services.CreateScope();
        var toManager = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .SingleAsync(item => item.UserId == 7);
        Assert.Contains("đã hủy đặt cọc phòng A101", toManager.Title);
    }

    [Fact]
    public async Task Move_in_reminders_reach_the_customer_and_the_assigned_manager()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        await CreatePaidBookingAsync(client); // move-in defaults to today (Vietnam)

        using var scope = factory.Services.CreateScope();
        var reminders = scope.ServiceProvider.GetRequiredService<PRN232.Application.Interfaces.IBookingMoveInReminderService>();
        var created = await reminders.SendDueRemindersAsync(CancellationToken.None);

        Assert.Equal(2, created);
        Assert.Equal(0, await reminders.SendDueRemindersAsync(CancellationToken.None));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Notifications.AnyAsync(item => item.UserId == 10 && item.Title.StartsWith("Ngày dọn vào phòng A101")));
        Assert.True(await db.Notifications.AnyAsync(item => item.UserId == 7 && item.Title.StartsWith("Khách Customer A dọn vào phòng A101")));
    }

    // Rebuild 3.2b: the manager pays the customer back outside the system and confirms it once.
    [Fact]
    public async Task Manager_confirms_the_deposit_was_returned_once_and_the_customer_is_told()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var bookingId = await CreatePaidBookingAsync(client);
        var confirmUrl = $"/api/bookings/{bookingId}/deposit-returned";
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(confirmUrl, null)).StatusCode); // not cancelled yet
        await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);

        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var byCustomer = await client.PostAsync(confirmUrl, null);
        await BookingsApiTests.LoginAsync(client, "manager-b@example.test");
        var byOtherManager = await client.PostAsync(confirmUrl, null);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        using var confirmed = await client.PostAsync(confirmUrl, null);
        var again = await client.PostAsync(confirmUrl, null);

        Assert.Equal(HttpStatusCode.Forbidden, byCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byOtherManager.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var booking = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(booking.GetProperty("depositRefundDue").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, booking.GetProperty("depositReturnedAtUtc").ValueKind);
        Assert.Equal("Manager A", booking.GetProperty("depositReturnedByName").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(7, (await db.Bookings.SingleAsync()).DepositReturnedById);
        var told = await db.Notifications.SingleAsync(item => item.UserId == 10 && item.Title.Contains("Đã xác nhận hoàn"));
        Assert.Contains("3.000.000đ", told.Body);
    }

    [Fact]
    public async Task Nothing_to_return_when_the_customer_cancelled()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var bookingId = await CreatePaidBookingAsync(client);
        await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);

        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        using var response = await client.PostAsync($"/api/bookings/{bookingId}/deposit-returned", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Null((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.SingleAsync()).DepositReturnedAtUtc);
    }

    private static async Task<int> CreatePaidBookingAsync(HttpClient client)
    {
        using var created = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bookingId").GetInt32();
        using var paid = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new { paymentMethod = "VNPay" });
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        return bookingId;
    }
}
