using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class ScopeBEndToEndReviewTests
{
    [Fact]
    public async Task User_directory_includes_active_manager_who_cannot_handle_this_property_viewing()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var viewing = await PostAsync(client, "/api/viewing-schedules", new
        {
            roomId = 1, scheduledAt = DateTimeOffset.UtcNow.AddDays(1)
        }, HttpStatusCode.Created);
        var viewingId = viewing.GetProperty("viewingId").GetInt32();
        await BookingsApiTests.LoginAsync(client, "admin@example.test");
        // This is the directory endpoint used to populate the FE's viewing-manager picker.
        var users = await client.GetFromJsonAsync<JsonElement>("/api/users?roleId=2");
        Assert.Contains(users.GetProperty("items").EnumerateArray(), row => row.GetProperty("userId").GetInt32() == 8
            && row.GetProperty("isActive").GetBoolean());
        using var response = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new
        {
            assignedTo = 8, scheduledAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("assignment", problem.GetProperty("detail").GetString());
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Pending", (await context.ViewingSchedules.SingleAsync()).Status);
        Assert.False(await context.PropertyManagerAssignments.AnyAsync(row => row.PropertyId == 1 && row.ManagerId == 8));
    }

    [Fact]
    public async Task Customer_deposit_admin_assigns_manager_contract_billing_complaint_and_checkout_are_consistent()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
        var month = new DateOnly(today.Year, today.Month, 1);
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        // Request the viewing while the room is rentable; an existing deposit prevents a new request.
        var viewing = await PostAsync(client, "/api/viewing-schedules", new
        {
            roomId = 1, scheduledAt = DateTimeOffset.UtcNow.AddDays(1)
        }, HttpStatusCode.Created);
        var viewingId = viewing.GetProperty("viewingId").GetInt32();
        var booking = await PostAsync(client, "/api/bookings", new { roomId = 1 }, HttpStatusCode.Created);
        var bookingId = booking.GetProperty("bookingId").GetInt32();
        var deposit = await PostAsync(client, $"/api/bookings/{bookingId}/pay", new { paymentMethod = "MoMo" });
        Assert.Equal("Deposited", deposit.GetProperty("status").GetString());

        await BookingsApiTests.LoginAsync(client, "admin@example.test");
        await PostAsync(client, "/api/properties/1/managers", new { managerId = 8 }, HttpStatusCode.Created);
        var approved = await PostAsync(client, $"/api/viewing-schedules/{viewingId}/approve", new
        {
            assignedTo = 8, scheduledAt = DateTimeOffset.UtcNow.AddDays(1), managerNote = "End-to-end review"
        });
        Assert.Equal("Approved", approved.GetProperty("status").GetString());

        await BookingsApiTests.LoginAsync(client, "manager-b@example.test");
        var contract = await PostAsync(client, "/api/contracts", new
        {
            bookingId, startDate = today, endDate = today.AddYears(1)
        }, HttpStatusCode.Created);
        var contractId = contract.GetProperty("contractId").GetInt32();
        using var handover = await client.PutAsJsonAsync($"/api/contracts/{contractId}/meter-handover", new
        {
            meterHandoverDate = today, initialElectricReading = 100m, initialWaterReading = 10m
        });
        Assert.Equal(HttpStatusCode.OK, handover.StatusCode);
        await PostAsync(client, "/api/utility-readings", new
        {
            contractId, readingMonth = month, electricStart = 100m, electricEnd = 150m, waterStart = 10m, waterEnd = 14m
        }, HttpStatusCode.Created);
        var invoice = await PostAsync(client, "/api/invoices", new
        {
            contractId, invoiceMonth = month, dueDate = today.AddDays(10)
        }, HttpStatusCode.Created);
        var invoiceId = invoice.GetProperty("invoiceId").GetInt32();
        Assert.Equal(3_000_000m, invoice.GetProperty("prepaidRentApplied").GetDecimal());
        Assert.Equal(355_000m, invoice.GetProperty("remainingPayable").GetDecimal());

        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var paid = await PostAsync(client, $"/api/invoices/{invoiceId}/pay", new { paymentMethod = "VNPay" });
        Assert.Equal("Paid", paid.GetProperty("status").GetString());
        var complaint = await PostAsync(client, "/api/invoice-complaints", new
        {
            invoiceId, feeType = "Khác", content = "Đề nghị kiểm tra cách tính tiền điện."
        }, HttpStatusCode.Created);
        var complaintId = complaint.GetProperty("complaintId").GetInt32();

        await BookingsApiTests.LoginAsync(client, "manager-b@example.test");
        using var resolved = await client.PatchAsJsonAsync($"/api/invoice-complaints/{complaintId}", new { status = "Resolved" });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var terminated = await PostAsync(client, $"/api/contracts/{contractId}/terminate", new
        {
            checkoutConfirmed = true, roomReadyForRental = true, outstandingDebtAcknowledged = false
        });
        Assert.Equal(0m, terminated.GetProperty("outstandingDebt").GetDecimal());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedContract = await context.Contracts.SingleAsync();
        Assert.Equal(("Terminated", (int?)8, (int?)bookingId),
            (savedContract.Status, savedContract.ProcessedByStaffId, savedContract.BookingId));
        Assert.Equal("Available", (await context.Rooms.SingleAsync(row => row.RoomId == 1)).Status);
        Assert.Equal("Paid", (await context.Invoices.SingleAsync()).Status);
        Assert.Equal("Resolved", (await context.InvoiceComplaints.SingleAsync()).Status);
        Assert.Equal(("Approved", (int?)8),
            await context.ViewingSchedules.Select(row => new ValueTuple<string, int?>(row.Status, row.AssignedTo)).SingleAsync());
        Assert.Equal(1, await context.UtilityReadings.CountAsync());
        Assert.Equal(2, await context.Payments.CountAsync());
        Assert.Equal(3_000_000m, (await context.Payments.SingleAsync(row => row.BookingId == bookingId)).Amount);
        Assert.Equal(355_000m, (await context.Payments.SingleAsync(row => row.InvoiceId == invoiceId)).Amount);
        Assert.Equal(-3_000_000m, (await context.InvoiceDetails.SingleAsync(row => row.ItemType == "PrepaidRentCredit")).Amount);
        Assert.True(await context.Notifications.AnyAsync(row => row.UserId == 8 && row.Type == "Viewing"));
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, string path, object body,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
