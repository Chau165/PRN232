using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;
using static PRN232.Application.Tests.ContractsApiTests;

namespace PRN232.Application.Tests;

public class InvoicesApiTests
{
    private static readonly DateOnly VietnamToday = DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
    private static readonly DateOnly ThisMonth = new(VietnamToday.Year, VietnamToday.Month, 1);
    private static readonly DateOnly DueDate = VietnamToday.AddDays(10);

    [Theory]
    [InlineData("GET", "/api/invoices")]
    [InlineData("POST", "/api/invoices")]
    [InlineData("POST", "/api/invoices/preview")]
    [InlineData("POST", "/api/invoices/1/pay")]
    [InlineData("POST", "/api/invoices/1/record-cash")]
    public async Task Anonymous_requests_require_authentication(string method, string path)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Assigned_manager_previews_then_bills_rent_usage_and_services_and_only_the_tenant_sees_it()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithReadingAsync(client);

        var preview = await client.PostAsJsonAsync("/api/invoices/preview", new
        {
            contractId,
            invoiceMonth = ThisMonth,
            dueDate = DueDate
        });
        int invoicesAfterPreview;
        using (var scope = factory.Services.CreateScope())
        {
            invoicesAfterPreview = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices.CountAsync();
        }
        var created = await client.PostAsJsonAsync("/api/invoices", new
        {
            contractId,
            invoiceMonth = ThisMonth,
            dueDate = DueDate
        });
        var again = await client.PostAsJsonAsync("/api/invoices", new
        {
            contractId,
            invoiceMonth = ThisMonth,
            dueDate = DueDate
        });

        // 3.000.000 tiền phòng được khấu trừ từ Booking; khách còn 355.000 tiền điện, nước và internet.
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(355_000m, previewBody.GetProperty("total").GetDecimal());
        Assert.Equal((3_000_000m, 355_000m),
            (previewBody.GetProperty("prepaidRentApplied").GetDecimal(), previewBody.GetProperty("netPayable").GetDecimal()));
        Assert.Equal(0, invoicesAfterPreview);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("Unpaid", 355_000m, 5), (body.GetProperty("status").GetString(), body.GetProperty("total").GetDecimal(),
            body.GetProperty("lines").GetArrayLength()));
        var creditLine = body.GetProperty("lines").EnumerateArray()
            .Single(line => line.GetProperty("itemType").GetString() == "PrepaidRentCredit");
        Assert.Equal(-3_000_000m, creditLine.GetProperty("amount").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 10 && notification.Type == "Invoice"));
        }

        await LoginAsync(client, "customer@example.test");
        var mine = await client.GetFromJsonAsync<JsonElement>("/api/invoices");
        await LoginAsync(client, "other@example.test");
        var someoneElse = await client.GetFromJsonAsync<JsonElement>("/api/invoices");
        Assert.Equal((1, 0), (mine.GetArrayLength(), someoneElse.GetArrayLength()));
        Assert.Equal(355_000m, mine[0].GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Assigned_managers_cannot_create_duplicate_invoice_credit_or_notification()
    {
        using var factory = new BookingsApiFactory();
        using var managerA = factory.CreateClient();
        using var managerB = factory.Server.CreateClient();
        var contractId = await CreateContractWithReadingAsync(managerA);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.PropertyManagerAssignments.Add(new PRN232.Domain.Entities.PropertyManagerAssignment
            {
                PropertyId = 1,
                ManagerId = 8
            });
            await context.SaveChangesAsync();
        }

        var first = await managerA.PostAsJsonAsync("/api/invoices", new
        {
            contractId,
            invoiceMonth = ThisMonth,
            dueDate = DueDate
        });
        await LoginAsync(managerB, "manager-b@example.test");
        var second = await managerB.PostAsJsonAsync("/api/invoices", new
        {
            contractId,
            invoiceMonth = ThisMonth,
            dueDate = DueDate
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await context.Invoices.CountAsync(invoice => invoice.ContractId == contractId && invoice.InvoiceMonth == ThisMonth));
            Assert.Equal(5, await context.InvoiceDetails.CountAsync(detail => detail.Invoice!.ContractId == contractId));
            Assert.Equal(1, await context.InvoiceDetails.CountAsync(detail =>
                detail.Invoice!.ContractId == contractId && detail.ItemType == "PrepaidRentCredit"));
            Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 10 && notification.Type == "Invoice"));
            Assert.Equal(1, await context.Payments.CountAsync(payment => payment.BookingId != null));
            Assert.Empty(await context.Payments.Where(payment => payment.InvoiceId != null).ToListAsync());
        }
        var realtime = factory.Services.GetRequiredService<CapturingNotificationRealtimePublisher>();
        Assert.Single(realtime.Created, notification => notification.Type == "Invoice");
    }

    [Fact]
    public async Task Invoice_needs_this_months_reading_and_a_future_due_date()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        var contract = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1));
        var contractId = (await contract.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();

        var noReading = await client.PostAsJsonAsync("/api/invoices", new
        {
            contractId,
            invoiceMonth = ThisMonth,
            dueDate = DueDate
        });
        var pastDue = await client.PostAsJsonAsync("/api/invoices", new
        {
            contractId,
            invoiceMonth = ThisMonth,
            dueDate = VietnamToday.AddDays(-1)
        });

        Assert.Equal((HttpStatusCode.Conflict, HttpStatusCode.BadRequest), (noReading.StatusCode, pastDue.StatusCode));
    }

    [Theory]
    [InlineData("manager-b@example.test")]
    [InlineData("customer@example.test")]
    [InlineData("admin@example.test")]
    public async Task Only_an_assigned_manager_can_bill(string email)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithReadingAsync(client);

        await LoginAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/invoices", new
        {
            contractId,
            invoiceMonth = ThisMonth,
            dueDate = DueDate
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_pays_online_once_and_the_owner_is_notified()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await CreateInvoiceAsync(client);

        await LoginAsync(client, "other@example.test");
        var byOtherTenant = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/pay", new
        {
            paymentMethod = "MoMo"
        });
        await LoginAsync(client, "customer@example.test");
        var paid = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/pay", new
        {
            paymentMethod = "MoMo"
        });
        var again = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/pay", new
        {
            paymentMethod = "MoMo"
        });
        await LoginAsync(client, "manager@example.test");
        var cashAfterPaid = await client.PostAsync($"/api/invoices/{invoiceId}/record-cash", null);

        Assert.Equal(HttpStatusCode.Forbidden, byOtherTenant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal("Paid", (await paid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal((HttpStatusCode.Conflict, HttpStatusCode.Conflict), (again.StatusCode, cashAfterPaid.StatusCode));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = await context.Payments.SingleAsync(item => item.InvoiceId == invoiceId);
        Assert.NotNull(payment.PaidAtUtc);
        Assert.Equal((10, 355_000m, "MoMo", "Success"), (payment.UserId, payment.Amount, payment.PaymentMethod, payment.Status));
        var ownerId = await context.Properties.Where(property => property.PropertyId == 1).Select(property => property.OwnerId).SingleAsync();
        Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == ownerId && notification.Type == "Invoice"));

    }

    [Fact]
    public async Task Another_tenant_is_forbidden_even_when_the_invoice_is_already_paid()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await CreateInvoiceAsync(client);

        await LoginAsync(client, "customer@example.test");
        var paid = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/pay", new
        {
            paymentMethod = "MoMo"
        });
        await LoginAsync(client, "other@example.test");
        var byOtherTenant = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/pay", new
        {
            paymentMethod = "MoMo"
        });

        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byOtherTenant.StatusCode);
    }

    [Theory]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.OK)]
    public async Task Only_an_assigned_manager_records_cash(string email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await CreateInvoiceAsync(client);

        await LoginAsync(client, email);
        var response = await client.PostAsync($"/api/invoices/{invoiceId}/record-cash", null);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            using var scope = factory.Services.CreateScope();
            var payment = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.SingleAsync(item => item.InvoiceId == invoiceId);
            Assert.Equal(("Cash", 10), (payment.PaymentMethod, payment.UserId));
        }
    }

    internal static async Task<int> CreateInvoiceAsync(HttpClient client)
    {
        var contractId = await CreateContractWithReadingAsync(client);
        var created = await client.PostAsJsonAsync("/api/invoices", new
        {
            contractId,
            invoiceMonth = ThisMonth,
            dueDate = DueDate
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceId").GetInt32();
    }

    [Fact]
    public async Task Cash_settlement_persists_time_and_retries_do_not_change_it()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await CreateInvoiceAsync(client);
        var before = DateTime.UtcNow;
        var response = await client.PostAsync($"/api/invoices/{invoiceId}/record-cash", null);
        var after = DateTime.UtcNow;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        DateTime paidAt;
        int paymentId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var payment = await context.Payments.SingleAsync(item => item.InvoiceId == invoiceId);
            Assert.NotNull(payment.PaidAtUtc);
            paidAt = payment.PaidAtUtc.Value;
            paymentId = payment.PaymentId;
            Assert.InRange(paidAt, before, after);
            Assert.Equal("Cash", payment.PaymentMethod);
            Assert.Equal("Success", payment.Status);
            Assert.Equal("Paid", (await context.Invoices.SingleAsync(item => item.InvoiceId == invoiceId)).Status);
        }
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/invoices/{invoiceId}/record-cash", null)).StatusCode);
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/pay", new { paymentMethod = "MoMo" })).StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var persisted = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Payments.SingleAsync(item => item.InvoiceId == invoiceId);
        Assert.Equal(paymentId, persisted.PaymentId);
        Assert.Equal(paidAt, persisted.PaidAtUtc);
    }

    // Hợp đồng bắt đầu hôm nay; chỉ số tháng này 100 -> 150 kWh và 10 -> 14 m3. Kết thúc ở tài khoản chủ khu trọ.

    private static async Task<int> CreateContractWithReadingAsync(HttpClient client)
    {
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        var contract = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1));
        var contractId = (await contract.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
        var handover = await client.PutAsJsonAsync($"/api/contracts/{contractId}/meter-handover", new
        {
            meterHandoverDate = VietnamToday, initialElectricReading = 100m, initialWaterReading = 10m
        });
        Assert.Equal(HttpStatusCode.OK, handover.StatusCode);
        var reading = await client.PostAsJsonAsync("/api/utility-readings", new
        {
            contractId,
            readingMonth = ThisMonth,
            electricStart = 100m,
            electricEnd = 150m,
            waterStart = 10m,
            waterEnd = 14m
        });
        Assert.Equal(HttpStatusCode.Created, reading.StatusCode);
        return contractId;
    }
}
