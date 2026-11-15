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
