using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class BookingRefundTrackingApiTests
{
    private static async Task<int> CreateAsync(BookingsApiFactory factory, HttpClient client)
    {
        var id = await BookingRefundRequestApiTests.SeedAsync(factory);
        await LoginAsync(client, "customer@example.test");
        var response = await client.PostAsJsonAsync($"/api/bookings/{id}/refund-requests", new CreateBookingRefundDto("Đổi kế hoạch", "970422", "001234567890", "TEST CUSTOMER"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return id;
    }

    [Theory]
    [InlineData("customer@example.test", HttpStatusCode.OK)]
    [InlineData("manager@example.test", HttpStatusCode.OK)]
    [InlineData("admin@example.test", HttpStatusCode.OK)]
    [InlineData("other@example.test", HttpStatusCode.NotFound)]
    [InlineData("manager-b@example.test", HttpStatusCode.NotFound)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task History_is_scoped_and_never_exposes_full_destination(string? email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await CreateAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = null; if (email is not null) await LoginAsync(client, email);
        var response = await client.GetAsync($"/api/bookings/{id}/refund-requests"); Assert.Equal(expected, response.StatusCode);
        if (expected != HttpStatusCode.OK) return;
        var history = (await response.Content.ReadFromJsonAsync<List<BookingRefundDto>>())!;
        Assert.Single(history); Assert.Equal("****7890", history[0].MaskedRecipientAccountNumber);
        Assert.DoesNotContain("001234567890", await response.Content.ReadAsStringAsync()); Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task Manager_loses_history_access_after_assignment_changes_and_new_manager_gains_access()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await CreateAsync(factory, client);
        await LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/bookings/{id}/refund-requests")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.PropertyManagerAssignments.RemoveRange(await db.PropertyManagerAssignments.ToListAsync());
            db.PropertyManagerAssignments.Add(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 }); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/bookings/{id}/refund-requests")).StatusCode);
        await LoginAsync(client, "manager-b@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/bookings/{id}/refund-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/bookings/{id}/refund-requests", new CreateBookingRefundDto("reason", "970422", "123", "name"))).StatusCode);
    }

    [Fact]
    public async Task Booking_summary_reflects_server_eligibility_and_active_refund_blocks_contract_actions()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await BookingRefundRequestApiTests.SeedAsync(factory);
        await LoginAsync(client, "customer@example.test");
        var initial = (await client.GetFromJsonAsync<BookingDto>($"/api/bookings/{id}"))!;
        Assert.True(initial.CanRequestRefund); Assert.Null(initial.RefundStatus);
        var created = await client.PostAsJsonAsync($"/api/bookings/{id}/refund-requests", new CreateBookingRefundDto("reason", "970422", "001234567890", "TEST CUSTOMER"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var current = (await client.GetFromJsonAsync<BookingDto>($"/api/bookings/{id}"))!;
        Assert.False(current.CanRequestRefund); Assert.Equal("PendingReview", current.RefundStatus);
        await LoginAsync(client, "manager@example.test");
        var list = (await client.GetFromJsonAsync<List<BookingDto>>("/api/bookings"))!;
        Assert.Equal("PendingReview", Assert.Single(list).RefundStatus);
    }
}
