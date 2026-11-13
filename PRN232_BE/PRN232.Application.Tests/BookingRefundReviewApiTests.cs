using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class BookingRefundReviewApiTests
{
    private const string Url = "/api/booking-refund-requests";
    private static async Task<int> CreateAsync(BookingsApiFactory factory, HttpClient client)
    {
        var bookingId = await BookingRefundRequestApiTests.SeedAsync(factory);
        await LoginAsync(client, "customer@example.test");
        var created = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/refund-requests",
            new CreateBookingRefundDto("Đổi kế hoạch", "970422", "001234567890", "TEST CUSTOMER"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<BookingRefundDto>())!.BookingRefundRequestId;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Decision_is_audited_notifies_customer_and_does_not_transfer_or_cancel(bool approve)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        var id = await CreateAsync(factory, client); await LoginAsync(client, "admin@example.test");
        var input = new ReviewBookingRefundDto(approve, approve ? 2500000m : null, " Đã đối chiếu ");
        var response = await client.PostAsJsonAsync($"{Url}/{id}/review", input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BookingRefundReviewDto>())!;
        Assert.Equal(approve ? "Approved" : "Rejected", body.Status);
        Assert.Equal(3000000m, body.DepositAmount); Assert.Equal(3000000m, body.RequestedAmount);
        Assert.Equal(input.ApprovedAmount, body.ApprovedAmount); Assert.Equal(9, body.ReviewedByAdminId);
        Assert.Equal("Đã đối chiếu", body.ReviewNote); Assert.Equal(DateTimeKind.Utc, body.ReviewedAtUtc!.Value.Kind);
        Assert.Equal("001234567890", body.RecipientAccountNumber); Assert.False(body.AccountNameVerified);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Url}/{id}/review", input)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Deposited", (await db.Bookings.SingleAsync()).Status);
        Assert.Equal(0, await db.PayoutAttempts.CountAsync()); Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(1, await db.Notifications.CountAsync(item => item.UserId == 10));
        Assert.Single((await client.GetFromJsonAsync<List<BookingRefundReviewDto>>($"{Url}?status={body.Status}"))!);
        Assert.Empty((await client.GetFromJsonAsync<List<BookingRefundReviewDto>>($"{Url}?status=PendingReview"))!);
        if (!approve)
        {
            await LoginAsync(client, "customer@example.test");
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/bookings/{body.BookingId}/refund-requests",
                new CreateBookingRefundDto("Xin xem lại", "970422", "001234567890", "TEST CUSTOMER"))).StatusCode);
        }
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.Forbidden)]
    public async Task Sensitive_reads_and_decisions_are_admin_only(string? email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        var id = await CreateAsync(factory, client); client.DefaultRequestHeaders.Authorization = null;
        if (email is not null) await LoginAsync(client, email);
        Assert.Equal(expected, (await client.GetAsync(Url)).StatusCode);
        Assert.Equal(expected, (await client.GetAsync($"{Url}/{id}")).StatusCode);
        Assert.Equal(expected, (await client.PostAsJsonAsync($"{Url}/{id}/review", new ReviewBookingRefundDto(false, null, "Từ chối"))).StatusCode);
    }

    [Theory]
    [InlineData(true, 0, "Reason", HttpStatusCode.BadRequest)]
    [InlineData(true, 1.5, "Reason", HttpStatusCode.BadRequest)]
    [InlineData(true, 3000001, "Reason", HttpStatusCode.Conflict)]
    [InlineData(false, 1, "Reason", HttpStatusCode.BadRequest)]
    [InlineData(true, 100, " ", HttpStatusCode.BadRequest)]
    public async Task Invalid_decision_has_no_side_effects(bool approve, double amount, string note, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        var id = await CreateAsync(factory, client); await LoginAsync(client, "admin@example.test");
        Assert.Equal(expected, (await client.PostAsJsonAsync($"{Url}/{id}/review", new ReviewBookingRefundDto(approve, (decimal)amount, note))).StatusCode);
        var body = await client.GetFromJsonAsync<BookingRefundReviewDto>($"{Url}/{id}");
        Assert.Equal("PendingReview", body!.Status); Assert.Null(body.ReviewedByAdminId);
    }

    [Fact]
    public async Task Concurrent_decisions_have_one_winner()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        var id = await CreateAsync(factory, client); await LoginAsync(client, "admin@example.test");
        var results = await Task.WhenAll(client.PostAsJsonAsync($"{Url}/{id}/review", new ReviewBookingRefundDto(true, 3000000, "Approve")),
            client.PostAsJsonAsync($"{Url}/{id}/review", new ReviewBookingRefundDto(false, null, "Reject")));
        Assert.Single(results, item => item.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, item => item.StatusCode == HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("evidence")]
    [InlineData("booking")]
    [InlineData("name")]
    public async Task Changed_deposit_evidence_blocks_approval_but_allows_rejection(string change)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        var id = await CreateAsync(factory, client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (change == "evidence") (await db.PaymentRequests.SingleAsync()).Status = "RequiresReview";
            if (change == "booking") (await db.Bookings.SingleAsync()).Status = "Cancelled";
            if (change == "name") (await db.BookingRefundRequests.SingleAsync()).RecipientAccountName = null;
            await db.SaveChangesAsync();
        }
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Url}/{id}/review", new ReviewBookingRefundDto(true, 3000000, "Approve"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{Url}/{id}/review", new ReviewBookingRefundDto(false, null, "Cần đối soát"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Url}?pageSize=101")).StatusCode);
    }
}
