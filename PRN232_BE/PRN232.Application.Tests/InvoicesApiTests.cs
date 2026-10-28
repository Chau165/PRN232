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
    public async Task Owner_previews_then_bills_rent_usage_and_services_and_only_the_tenant_sees_it()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithReadingAsync(client);

        var preview = await client.PostAsJsonAsync("/api/invoices/preview", new { contractId, invoiceMonth = ThisMonth, dueDate = DueDate });
        int invoicesAfterPreview;
        using (var scope = factory.Services.CreateScope())
        {
            invoicesAfterPreview = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices.CountAsync();
        }
        var created = await client.PostAsJsonAsync("/api/invoices", new { contractId, invoiceMonth = ThisMonth, dueDate = DueDate });
        var again = await client.PostAsJsonAsync("/api/invoices", new { contractId, invoiceMonth = ThisMonth, dueDate = DueDate });

        // 3.000.000 tiền phòng + 50 kWh x 3.500 + 4 m3 x 20.000 + 100.000 internet.
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(3_355_000m, (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
        Assert.Equal(0, invoicesAfterPreview);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("Unpaid", 3_355_000m, 4), (body.GetProperty("status").GetString(), body.GetProperty("total").GetDecimal(),
            body.GetProperty("lines").GetArrayLength()));
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
        Assert.Equal(3_355_000m, mine[0].GetProperty("total").GetDecimal());
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

        var noReading = await client.PostAsJsonAsync("/api/invoices", new { contractId, invoiceMonth = ThisMonth, dueDate = DueDate });
        var pastDue = await client.PostAsJsonAsync("/api/invoices", new { contractId, invoiceMonth = ThisMonth, dueDate = VietnamToday.AddDays(-1) });

        Assert.Equal((HttpStatusCode.Conflict, HttpStatusCode.BadRequest), (noReading.StatusCode, pastDue.StatusCode));
    }

    [Theory]
    [InlineData("manager-b@example.test")]
    [InlineData("customer@example.test")]
    public async Task Only_the_owner_or_an_admin_can_bill(string email)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithReadingAsync(client);

        await LoginAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/invoices", new { contractId, invoiceMonth = ThisMonth, dueDate = DueDate });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_pays_online_once_and_the_owner_is_notified()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await CreateInvoiceAsync(client);

        await LoginAsync(client, "other@example.test");
        var byOtherTenant = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/pay", new { paymentMethod = "MoMo" });
        await LoginAsync(client, "customer@example.test");
        var paid = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/pay", new { paymentMethod = "MoMo" });
        var again = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/pay", new { paymentMethod = "MoMo" });
        await LoginAsync(client, "manager@example.test");
        var cashAfterPaid = await client.PostAsync($"/api/invoices/{invoiceId}/record-cash", null);

        Assert.Equal(HttpStatusCode.Forbidden, byOtherTenant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal("Paid", (await paid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal((HttpStatusCode.Conflict, HttpStatusCode.Conflict), (again.StatusCode, cashAfterPaid.StatusCode));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = await context.Payments.SingleAsync(item => item.InvoiceId == invoiceId);
        Assert.Equal((10, 3_355_000m, "MoMo", "Success"), (payment.UserId, payment.Amount, payment.PaymentMethod, payment.Status));
        Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 7 && notification.Type == "Invoice"));
    }

    [Theory]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.OK)]
    public async Task Only_the_owner_or_an_admin_records_cash(string email, HttpStatusCode expected)
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

    private static async Task<int> CreateInvoiceAsync(HttpClient client)
    {
        var contractId = await CreateContractWithReadingAsync(client);
        var created = await client.PostAsJsonAsync("/api/invoices", new { contractId, invoiceMonth = ThisMonth, dueDate = DueDate });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceId").GetInt32();
    }

    // Hợp đồng bắt đầu hôm nay; chỉ số tháng này 100 -> 150 kWh và 10 -> 14 m3. Kết thúc ở tài khoản chủ khu trọ.
    private static async Task<int> CreateContractWithReadingAsync(HttpClient client)
    {
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        var contract = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1));
        var contractId = (await contract.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
        var reading = await client.PostAsJsonAsync("/api/utility-readings", new
        {
            contractId, readingMonth = ThisMonth, electricStart = 100m, electricEnd = 150m, waterStart = 10m, waterEnd = 14m
        });
        Assert.Equal(HttpStatusCode.Created, reading.StatusCode);
        return contractId;
    }
}
