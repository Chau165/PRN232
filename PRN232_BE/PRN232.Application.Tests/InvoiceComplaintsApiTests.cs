using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;
using static PRN232.Application.Tests.InvoicesApiTests;

namespace PRN232.Application.Tests;

public class InvoiceComplaintsApiTests
{
    [Theory]
    [InlineData("GET", "/api/invoice-complaints")]
    [InlineData("POST", "/api/invoice-complaints")]
    [InlineData("PATCH", "/api/invoice-complaints/1")]
    public async Task Anonymous_requests_require_authentication(string method, string path)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    // Mỗi test đăng nhập tối đa 5 lần: LoginPolicy của nhóm chỉ cho 5 lần đăng nhập mỗi phút.
    [Fact]
    public async Task Tenant_complains_only_about_a_real_line_of_their_own_invoice()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await CreateInvoiceAsync(client);
        await LoginAsync(client, "customer@example.test");
        var invoice = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{invoiceId}");
        var rentLine = invoice.GetProperty("lines")[0].GetProperty("description").GetString();

        var invented = await client.PostAsJsonAsync("/api/invoice-complaints", new
        {
            invoiceId,
            feeType = "Tiền gửi xe",
            content = "Sai"
        });
        var created = await client.PostAsJsonAsync("/api/invoice-complaints", new
        {
            invoiceId,
            feeType = rentLine,
            content = "Giá phòng không đúng hợp đồng"
        });
        await LoginAsync(client, "other@example.test");
        var byOtherTenant = await client.PostAsJsonAsync("/api/invoice-complaints", new
        {
            invoiceId,
            feeType = "Khác",
            content = "Sai"
        });

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.Created, HttpStatusCode.Forbidden),
            (invented.StatusCode, created.StatusCode, byOtherTenant.StatusCode));
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("New", rentLine), (body.GetProperty("status").GetString(), body.GetProperty("feeType").GetString()));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await context.Notifications.CountAsync(item => item.UserId == 7 && item.Type == "Complaint"));
    }

    [Fact]
    public async Task Only_the_owner_moves_a_complaint_forward_and_the_tenant_is_notified()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await CreateInvoiceAsync(client);
        await LoginAsync(client, "customer@example.test");
        var created = await client.PostAsJsonAsync("/api/invoice-complaints", new
        {
            invoiceId,
            feeType = "Khác",
            content = "Sai"
        });
        var complaintId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("complaintId").GetInt32();

        await LoginAsync(client, "manager-b@example.test");
        var byOtherManager = await client.PatchAsJsonAsync($"/api/invoice-complaints/{complaintId}", new
        {
            status = "Resolved"
        });
        await LoginAsync(client, "manager@example.test");
        var handled = await client.PatchAsJsonAsync($"/api/invoice-complaints/{complaintId}", new
        {
            status = "InProgress"
        });

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.OK), (byOtherManager.StatusCode, handled.StatusCode));
        Assert.Equal("InProgress", (await handled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await context.Notifications.CountAsync(item => item.UserId == 10 && item.Type == "Complaint"));
    }

    [Theory]
    [InlineData("customer@example.test", 1)]
    [InlineData("other@example.test", 0)]
    [InlineData("manager@example.test", 1)]
    [InlineData("manager-b@example.test", 0)]
    [InlineData("admin@example.test", 1)]
    public async Task Complaint_list_follows_customer_owner_and_admin_scope(string email, int expectedCount)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await CreateInvoiceAsync(client);
        await LoginAsync(client, "customer@example.test");
        var created = await client.PostAsJsonAsync("/api/invoice-complaints", new
        {
            invoiceId,
            feeType = "Khác",
            content = "Hạn thanh toán quá gấp"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        await LoginAsync(client, email);
        var complaints = await client.GetFromJsonAsync<JsonElement>("/api/invoice-complaints");

        Assert.Equal(expectedCount, complaints.GetArrayLength());
    }
}
