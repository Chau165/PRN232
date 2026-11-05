using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class ContractsApiTests
{
    private static readonly DateOnly VietnamToday = DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);

    [Theory]
    [InlineData("GET", "/api/contracts")]
    [InlineData("POST", "/api/contracts")]
    public async Task Anonymous_requests_require_authentication(string method, string path)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Assigned_manager_turns_a_paid_booking_into_a_contract_that_the_tenant_sees_as_a_current_rental()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");

        var response = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1));
        var again = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1));
        var cancel = await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("Active", "A101", "Manager A"), (body.GetProperty("status").GetString(),
            body.GetProperty("roomCode").GetString(), body.GetProperty("processedByStaffName").GetString()));
        Assert.Equal((3_000_000m, 3_000_000m), (body.GetProperty("monthlyRent").GetDecimal(), body.GetProperty("depositAmount").GetDecimal()));
        Assert.Equal(VietnamToday.ToString("yyyy-MM-dd"), body.GetProperty("startDate").GetString());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Occupied", (await context.Rooms.SingleAsync(room => room.RoomId == 1)).Status);
            Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 10 && notification.Type == "Contract"));
        }

        await LoginAsync(client, "customer@example.test");
        var booking = await client.GetFromJsonAsync<JsonElement>($"/api/bookings/{bookingId}");
        var rentals = await client.GetFromJsonAsync<JsonElement>("/api/me/rentals");
        Assert.Equal(body.GetProperty("contractId").GetInt32(), booking.GetProperty("contractId").GetInt32());
        Assert.Equal(1, rentals.GetArrayLength());
    }

    [Theory]
    [InlineData("admin@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.Created)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_an_assigned_manager_can_create_a_contract(string email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);

        await LoginAsync(client, email);
        var response = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddMonths(6));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Unpaid_or_unknown_bookings_are_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var unpaid = await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = 1
        });
        var unpaidId = (await unpaid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bookingId").GetInt32();
        await LoginAsync(client, "manager@example.test");

        var fromUnpaid = await PostContractAsync(client, unpaidId, VietnamToday, VietnamToday.AddYears(1));
        var unknown = await PostContractAsync(client, 999, VietnamToday, VietnamToday.AddYears(1));

        Assert.Equal(HttpStatusCode.Conflict, fromUnpaid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Theory]
    [InlineData(-1, 365)]
    [InlineData(0, 0)]
    public async Task Start_date_in_the_past_or_end_not_after_start_is_rejected(int startOffsetDays, int termDays)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        var start = VietnamToday.AddDays(startOffsetDays);

        var response = await PostContractAsync(client, bookingId, start, start.AddDays(termDays));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("customer@example.test", 1)]
    [InlineData("other@example.test", 0)]
    [InlineData("manager@example.test", 1)]
    [InlineData("manager-b@example.test", 0)]
    [InlineData("admin@example.test", 1)]
    public async Task Contract_list_follows_customer_manager_and_admin_scope(string email, int expectedCount)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Created, (await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1))).StatusCode);

        await LoginAsync(client, email);
        var contracts = await client.GetFromJsonAsync<JsonElement>("/api/contracts");

        Assert.Equal(expectedCount, contracts.GetArrayLength());
    }

    [Fact]
    public async Task Billing_timeline_returns_empty_invoice_list_and_keeps_customer_scope()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        using var created = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var contractId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("contractId")
            .GetInt32();

        await LoginAsync(client, "customer@example.test");
        using var timelineResponse = await client.GetAsync($"/api/contracts/{contractId}/billing-timeline");
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        var timeline = await timelineResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(timeline.GetProperty("issuedInvoices").EnumerateArray());

        int invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = new Invoices
            {
                ContractId = contractId,
                InvoiceMonth = VietnamToday.AddDays(1 - VietnamToday.Day),
                DueDate = VietnamToday.AddMonths(1),
                Status = InvoiceStatuses.Unpaid
            };
            invoice.InvoiceDetails.Add(new InvoiceDetails
            {
                ItemType = InvoiceItemTypes.Rent,
                Description = "Rent",
                Amount = 1_000m
            });
            invoice.InvoiceDetails.Add(new InvoiceDetails
            {
                ItemType = InvoiceItemTypes.PrepaidRentCredit,
                Description = "Booking credit",
                Amount = -200m
            });
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            invoiceId = invoice.InvoiceId;
            context.Payments.AddRange(
                new Payments
                {
                    UserId = 10,
                    InvoiceId = invoiceId,
                    Amount = 100m,
                    PaymentMethod = "VNPay",
                    Status = PaymentStatuses.Success
                },
                new Payments
                {
                    UserId = 10,
                    InvoiceId = invoiceId,
                    Amount = 50m,
                    PaymentMethod = "MoMo",
                    Status = PaymentStatuses.Success
                });
            await context.SaveChangesAsync();
        }

        using var populatedTimelineResponse = await client.GetAsync($"/api/contracts/{contractId}/billing-timeline");
        Assert.Equal(HttpStatusCode.OK, populatedTimelineResponse.StatusCode);
        var populatedTimeline = await populatedTimelineResponse.Content.ReadFromJsonAsync<JsonElement>();
        var issuedInvoice = Assert.Single(populatedTimeline.GetProperty("issuedInvoices").EnumerateArray());
        Assert.Equal(invoiceId, issuedInvoice.GetProperty("invoiceId").GetInt32());
        Assert.Equal(800m, issuedInvoice.GetProperty("total").GetDecimal());
        Assert.Equal(200m, issuedInvoice.GetProperty("prepaidRentApplied").GetDecimal());
        Assert.Equal(800m, issuedInvoice.GetProperty("netPayable").GetDecimal());
        Assert.Equal(150m, issuedInvoice.GetProperty("amountPaid").GetDecimal());
        Assert.Equal(650m, issuedInvoice.GetProperty("remainingPayable").GetDecimal());

        await LoginAsync(client, "other@example.test");
        using var forbidden = await client.GetAsync($"/api/contracts/{contractId}/billing-timeline");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Assigned_manager_extends_the_existing_contract_and_customer_sees_the_new_billing_term()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        using var created = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddMonths(6));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var contractId = createdBody.GetProperty("contractId").GetInt32();
        var newEndDate = VietnamToday.AddMonths(18);

        using var extended = await client.PutAsJsonAsync($"/api/contracts/{contractId}/extend", new
        {
            newEndDate
        });

        Assert.Equal(HttpStatusCode.OK, extended.StatusCode);
        var body = await extended.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((contractId, ContractStatuses.Active, newEndDate.ToString("yyyy-MM-dd")),
            (body.GetProperty("contractId").GetInt32(),
             body.GetProperty("status").GetString(),
             body.GetProperty("endDate").GetString()));
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await context.Contracts.CountAsync());
            Assert.Equal(1, await context.Bookings.CountAsync());
            Assert.Equal(1, await context.Payments.CountAsync());
            Assert.Equal(RoomStatuses.Occupied,
                (await context.Rooms.SingleAsync(room => room.RoomId == 1)).Status);
        }

        await LoginAsync(client, "customer@example.test");
        var contracts = await client.GetFromJsonAsync<JsonElement>("/api/contracts");
        Assert.Equal(newEndDate.ToString("yyyy-MM-dd"),
            Assert.Single(contracts.EnumerateArray()).GetProperty("endDate").GetString());
        var timeline = await client.GetFromJsonAsync<JsonElement>($"/api/contracts/{contractId}/billing-timeline");
        Assert.Contains(timeline.GetProperty("plannedBillingPeriods").EnumerateArray(), period =>
            period.GetProperty("invoiceMonth").GetString() ==
            new DateOnly(newEndDate.Year, newEndDate.Month, 1).ToString("yyyy-MM-dd"));
    }

    [Theory]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_an_assigned_manager_can_extend_or_terminate_contracts(
        string email,
        HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);
        await LoginAsync(client, email);

        using var extend = await client.PutAsJsonAsync($"/api/contracts/{contractId}/extend", new
        {
            newEndDate = VietnamToday.AddMonths(18)
        });
        using var terminate = await client.PostAsJsonAsync($"/api/contracts/{contractId}/terminate", new
        {
            checkoutConfirmed = true,
            roomReadyForRental = true,
            outstandingDebtAcknowledged = true
        });

        Assert.Equal(expected, extend.StatusCode);
        Assert.Equal(expected, terminate.StatusCode);
    }

    [Fact]
    public async Task Expired_active_contract_waits_for_checkout_and_remains_occupied_until_extended_or_terminated()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Contracts.Where(contract => contract.ContractId == contractId)
                .ExecuteUpdateAsync(update => update.SetProperty(contract => contract.EndDate, VietnamToday.AddDays(-1)));
        }

        await LoginAsync(client, "customer@example.test");
        var customerContracts = await client.GetFromJsonAsync<JsonElement>("/api/contracts");
        Assert.Equal("ExpiredPendingCheckout",
            Assert.Single(customerContracts.EnumerateArray()).GetProperty("displayStatus").GetString());
        using var competingBooking = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });
        Assert.Equal(HttpStatusCode.Conflict, competingBooking.StatusCode);

        await LoginAsync(client, "manager@example.test");
        using var extended = await client.PutAsJsonAsync($"/api/contracts/{contractId}/extend", new
        {
            newEndDate = VietnamToday.AddMonths(6)
        });
        Assert.Equal(HttpStatusCode.OK, extended.StatusCode);
        using var scopeAfter = factory.Services.CreateScope();
        var updatedRoom = await scopeAfter.ServiceProvider.GetRequiredService<AppDbContext>().Rooms
            .SingleAsync(room => room.RoomId == 1);
        Assert.Equal(RoomStatuses.Occupied, updatedRoom.Status);
    }

    [Fact]
    public async Task Manager_can_terminate_with_debt_and_customer_can_pay_after_checkout_without_reoccupying_room()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);
        int onlineInvoiceId;
        int cashInvoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            onlineInvoiceId = await AddInvoiceAsync(
                context,
                contractId,
                new DateOnly(VietnamToday.Year, VietnamToday.Month, 1),
                3_237_500m);
            cashInvoiceId = await AddInvoiceAsync(
                context,
                contractId,
                new DateOnly(VietnamToday.Year, VietnamToday.Month, 1).AddMonths(-1),
                1_000_000m);
        }

        using var terminated = await client.PostAsJsonAsync($"/api/contracts/{contractId}/terminate", new
        {
            checkoutConfirmed = true,
            roomReadyForRental = true,
            outstandingDebtAcknowledged = true
        });
        Assert.Equal(HttpStatusCode.OK, terminated.StatusCode);
        var terminationBody = await terminated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ContractStatuses.Terminated,
            terminationBody.GetProperty("contract").GetProperty("status").GetString());
        Assert.Equal(4_237_500m, terminationBody.GetProperty("outstandingDebt").GetDecimal());

        using var invoiceAfterTermination = await client.PostAsJsonAsync("/api/invoices", new
        {
            contractId,
            invoiceMonth = new DateOnly(VietnamToday.Year, VietnamToday.Month, 1),
            dueDate = VietnamToday.AddDays(10)
        });
        Assert.Equal(HttpStatusCode.Conflict, invoiceAfterTermination.StatusCode);

        using var duplicateTermination = await client.PostAsJsonAsync($"/api/contracts/{contractId}/terminate", new
        {
            checkoutConfirmed = true,
            roomReadyForRental = true,
            outstandingDebtAcknowledged = true
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateTermination.StatusCode);

        await LoginAsync(client, "customer@example.test");
        var invoices = await client.GetFromJsonAsync<JsonElement>("/api/invoices");
        Assert.Contains(invoices.EnumerateArray(), invoice => invoice.GetProperty("invoiceId").GetInt32() == onlineInvoiceId);
        using var onlinePayment = await client.PostAsJsonAsync($"/api/invoices/{onlineInvoiceId}/pay", new
        {
            paymentMethod = "VNPay"
        });
        Assert.Equal(HttpStatusCode.OK, onlinePayment.StatusCode);

        await LoginAsync(client, "manager@example.test");
        using var cashPayment = await client.PostAsync($"/api/invoices/{cashInvoiceId}/record-cash", null);
        Assert.Equal(HttpStatusCode.OK, cashPayment.StatusCode);
        using var duplicateCashPayment = await client.PostAsync($"/api/invoices/{cashInvoiceId}/record-cash", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicateCashPayment.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(ContractStatuses.Terminated,
                await context.Contracts.Where(contract => contract.ContractId == contractId)
                    .Select(contract => contract.Status).SingleAsync());
            Assert.Equal(RoomStatuses.Available,
                (await context.Rooms.SingleAsync(room => room.RoomId == 1)).Status);
            Assert.All(await context.Invoices.ToListAsync(), invoice => Assert.Equal(InvoiceStatuses.Paid, invoice.Status));
            Assert.Equal(2, await context.Payments.CountAsync(payment => payment.InvoiceId != null));
            Assert.Equal(1, await context.Notifications.CountAsync(notification =>
                notification.UserId == 10 && notification.Title.Contains("kết thúc")));
        }

        await LoginAsync(client, "other@example.test");
        using var newBooking = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });
        Assert.Equal(HttpStatusCode.Created, newBooking.StatusCode);
    }

    [Fact]
    public async Task Extension_is_rejected_when_another_live_booking_holds_the_room()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Bookings.Add(new Bookings
            {
                RoomId = 1,
                CustomerId = 20,
                Status = BookingStatuses.Pending,
                ExpiryDate = DateTime.UtcNow.AddDays(2)
            });
            await context.SaveChangesAsync();
        }

        await LoginAsync(client, "manager@example.test");
        using var response = await client.PutAsJsonAsync($"/api/contracts/{contractId}/extend", new
        {
            newEndDate = VietnamToday.AddMonths(18)
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var scopeAfter = factory.Services.CreateScope();
        Assert.Equal(VietnamToday.AddYears(1), await scopeAfter.ServiceProvider.GetRequiredService<AppDbContext>()
            .Contracts.Where(contract => contract.ContractId == contractId)
            .Select(contract => contract.EndDate)
            .SingleAsync());
    }

    [Fact]
    public async Task Assigned_manager_can_mark_a_terminated_room_ready_after_cleanup()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);
        await LoginAsync(client, "manager@example.test");
        using var terminated = await client.PostAsJsonAsync($"/api/contracts/{contractId}/terminate", new
        {
            checkoutConfirmed = true,
            roomReadyForRental = false,
            outstandingDebtAcknowledged = true
        });
        Assert.Equal(HttpStatusCode.OK, terminated.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            Assert.Equal(RoomStatuses.Maintenance,
                (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Rooms
                    .SingleAsync(room => room.RoomId == 1)).Status);
        }

        using var ready = await client.PostAsync($"/api/contracts/{contractId}/room-ready", null);

        Assert.Equal(HttpStatusCode.NoContent, ready.StatusCode);
        using var scopeAfter = factory.Services.CreateScope();
        Assert.Equal(RoomStatuses.Available,
            (await scopeAfter.ServiceProvider.GetRequiredService<AppDbContext>().Rooms
                .SingleAsync(room => room.RoomId == 1)).Status);
    }

    internal static Task<HttpResponseMessage> PostContractAsync(HttpClient client, int bookingId, DateOnly startDate, DateOnly endDate) =>
        client.PostAsJsonAsync("/api/contracts", new
        {
            bookingId,
            startDate,
            endDate
        });

    private static async Task<int> CreateContractAsync(HttpClient client)
    {
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        using var response = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
    }

    private static async Task<int> AddInvoiceAsync(
        AppDbContext context,
        int contractId,
        DateOnly invoiceMonth,
        decimal amount)
    {
        var invoice = new Invoices
        {
            ContractId = contractId,
            InvoiceMonth = invoiceMonth,
            DueDate = VietnamToday.AddDays(-1),
            Status = InvoiceStatuses.Overdue
        };
        invoice.InvoiceDetails.Add(new InvoiceDetails
        {
            ItemType = InvoiceItemTypes.Rent,
            Description = "Rent and utility charges",
            Amount = amount
        });
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        return invoice.InvoiceId;
    }

    internal static async Task<int> CreatePaidBookingAsync(HttpClient client)
    {
        await LoginAsync(client, "customer@example.test");
        var created = await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = 1
        });
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bookingId").GetInt32();
        var paid = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new
        {
            paymentMethod = "VNPay"
        });
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        return bookingId;
    }
}
