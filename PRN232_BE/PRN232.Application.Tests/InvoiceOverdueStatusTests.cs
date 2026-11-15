using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Policies;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

// B5: invoices past their due date read as Overdue, without rewriting stored rows.
public sealed class InvoiceOverdueStatusTests
{
    private static DateOnly Today => InvoiceOverduePolicy.Today(TimeProvider.System);

    [Theory]
    [InlineData("Unpaid", -1, "Overdue")]
    [InlineData("Unpaid", 0, "Unpaid")]
    [InlineData("Unpaid", 5, "Unpaid")]
    [InlineData("Overdue", -1, "Overdue")]
    [InlineData("Overdue", 3, "Unpaid")]
    [InlineData("Paid", -30, "Paid")]
    public void Effective_status_follows_the_due_date_in_vietnam_time(string stored, int dueInDays, string expected)
    {
        var today = new DateOnly(2026, 11, 16);

        Assert.Equal(expected, InvoiceOverduePolicy.EffectiveStatus(stored, today.AddDays(dueInDays), today));
    }

    [Fact]
    public async Task Due_yesterday_reads_as_overdue_and_due_today_as_unpaid_without_rewriting_rows()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var (yesterdayId, todayId) = await SeedTwoInvoicesAsync(factory, client);

        var all = await client.GetFromJsonAsync<InvoiceDto[]>("/api/invoices");
        Assert.Equal("Overdue", all!.Single(invoice => invoice.InvoiceId == yesterdayId).Status);
        Assert.Equal("Unpaid", all.Single(invoice => invoice.InvoiceId == todayId).Status);
        var detail = await client.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{yesterdayId}");
        Assert.Equal("Overdue", detail!.Status);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.All(await context.Invoices.ToListAsync(), invoice => Assert.Equal(InvoiceStatuses.Unpaid, invoice.Status));
    }

    [Fact]
    public async Task Status_filters_split_overdue_and_unpaid_rows()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var (yesterdayId, todayId) = await SeedTwoInvoicesAsync(factory, client);

        var overdue = await client.GetFromJsonAsync<InvoiceDto[]>("/api/invoices?status=Overdue");
        var unpaid = await client.GetFromJsonAsync<InvoiceDto[]>("/api/invoices?status=Unpaid");

        Assert.Equal([yesterdayId], overdue!.Select(invoice => invoice.InvoiceId));
        Assert.All(overdue, invoice => Assert.Equal("Overdue", invoice.Status));
        Assert.Equal([todayId], unpaid!.Select(invoice => invoice.InvoiceId));
        Assert.All(unpaid, invoice => Assert.Equal("Unpaid", invoice.Status));
    }

    [Fact]
    public async Task Paying_an_overdue_invoice_online_and_in_cash_still_succeeds()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var (yesterdayId, todayId) = await SeedTwoInvoicesAsync(factory, client);
        using (var scope = factory.Services.CreateScope())
        {
            // Make both invoices overdue so each payment path is exercised on an Overdue invoice.
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await context.Invoices.SingleAsync(invoice => invoice.InvoiceId == todayId)).DueDate = Today.AddDays(-2);
            await context.SaveChangesAsync();
        }

        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        using var online = await client.PostAsJsonAsync($"/api/invoices/{yesterdayId}/pay", new { paymentMethod = "MoMo" });
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        using var cash = await client.PostAsync($"/api/invoices/{todayId}/record-cash", null);

        Assert.Equal(HttpStatusCode.OK, online.StatusCode);
        Assert.Equal("Paid", (await online.Content.ReadFromJsonAsync<InvoiceDto>())!.Status);
        Assert.Equal(HttpStatusCode.OK, cash.StatusCode);
        Assert.Equal("Paid", (await cash.Content.ReadFromJsonAsync<InvoiceDto>())!.Status);
    }

    // One invoice from the normal API flow (due yesterday after the update) and one for the next
    // month on the same contract, due today. The client is left logged in as the Manager.
    private static async Task<(int YesterdayId, int TodayId)> SeedTwoInvoicesAsync(
        BookingsApiFactory factory, HttpClient client)
    {
        var yesterdayId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await context.Invoices.SingleAsync(invoice => invoice.InvoiceId == yesterdayId);
        first.DueDate = Today.AddDays(-1);
        var second = new Invoices
        {
            ContractId = first.ContractId,
            InvoiceMonth = first.InvoiceMonth.AddMonths(1),
            DueDate = Today,
            Status = InvoiceStatuses.Unpaid
        };
        second.InvoiceDetails.Add(new InvoiceDetails
        {
            ItemType = InvoiceItemTypes.Rent,
            Description = "Rent",
            Amount = 3_000_000m
        });
        context.Invoices.Add(second);
        await context.SaveChangesAsync();
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        return (yesterdayId, second.InvoiceId);
    }
}
