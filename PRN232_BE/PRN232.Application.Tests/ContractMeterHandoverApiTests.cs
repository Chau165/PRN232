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

public class ContractMeterHandoverApiTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
        DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
    private static readonly DateOnly Month = new(Today.Year, Today.Month, 1);

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.OK)]
    public async Task Only_the_assigned_manager_can_save(string? email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await CreateAsync(client);
        if (email is null)
        {
            client.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            await LoginAsync(client, email);
        }
        Assert.Equal(expected, (await SaveAsync(client, id)).StatusCode);
    }

    [Fact]
    public async Task Missing_baseline_cannot_be_bypassed_with_client_start_values()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await CreateAsync(client);
        var response = await client.PostAsJsonAsync("/api/utility-readings", new
        {
            contractId = id, readingMonth = Month, electricEnd = 1248, waterEnd = 49,
            electricStart = 1200, waterStart = 45
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("bàn giao", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Save_is_idempotent_creates_no_reading_and_locks_after_finalization()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await CreateAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(client, id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(client, id)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Empty(await db.UtilityReadings.ToListAsync());
            Assert.Empty(await db.Invoices.ToListAsync());
        }
        var saved = await client.PostAsJsonAsync("/api/utility-readings", new
        {
            contractId = id, readingMonth = Month, electricEnd = 1248, waterEnd = 49
        });
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var reading = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1200m, reading.GetProperty("electricStart").GetDecimal());
        Assert.Equal(45m, reading.GetProperty("waterStart").GetDecimal());
        Assert.Equal(48m, reading.GetProperty("electricUsage").GetDecimal());
        Assert.Equal(4m, reading.GetProperty("waterUsage").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await SaveAsync(client, id, 1201)).StatusCode);
        await LoginAsync(client, "customer@example.test");
        var contract = await client.GetFromJsonAsync<JsonElement>($"/api/contracts/{id}");
        Assert.Equal(1200m, contract.GetProperty("initialElectricReading").GetDecimal());
        Assert.True(contract.GetProperty("hasMeterHandover").GetBoolean());
    }

    [Fact]
    public async Task Editing_requires_the_baseline_that_was_read_and_rejects_stale_updates()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await CreateAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(client, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SaveAsync(client, id, 1201)).StatusCode);
        var expected = new { meterHandoverDate = Today, initialElectricReading = 1200, initialWaterReading = 45 };
        var request = new { meterHandoverDate = Today, initialElectricReading = 1201, initialWaterReading = 45, expectedMeterHandover = expected };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/contracts/{id}/meter-handover", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/contracts/{id}/meter-handover", request with { initialElectricReading = 1202 })).StatusCode);
    }

    [Theory]
    [InlineData(-1, 45, 0)]
    [InlineData(1200, -1, 0)]
    [InlineData(1200, 45, -1)]
    [InlineData(1200, 45, 1)]
    [InlineData(1200, 45, 367)]
    [InlineData(1200.123, 45, 0)]
    [InlineData(1200, 45.123, 0)]
    public async Task Invalid_values_and_dates_are_rejected(decimal electric, decimal water, int offset)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await CreateAsync(client);
        var response = await client.PutAsJsonAsync($"/api/contracts/{id}/meter-handover", new
        {
            meterHandoverDate = Today.AddDays(offset), initialElectricReading = electric, initialWaterReading = water
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_fields_missing_contract_and_client_override_are_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await CreateAsync(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/contracts/{id}/meter-handover", new { meterHandoverDate = Today })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SaveAsync(client, 999)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(client, id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/utility-readings", new
        {
            contractId = id, readingMonth = Month, electricEnd = 1248, waterEnd = 49, electricStart = 0, waterStart = 0
        })).StatusCode);
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, int id, decimal electric = 1200) =>
        client.PutAsJsonAsync($"/api/contracts/{id}/meter-handover", new
        {
            meterHandoverDate = Today, initialElectricReading = electric, initialWaterReading = 45
        });

    [Fact]
    public async Task First_invoice_keeps_booking_credit_and_bills_48_kwh_and_4_cubic_meters()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await CreateAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fixedServiceIds = await db.PropertyServiceRates.Where(rate => rate.BillingBasis != "usage")
                .Select(rate => rate.ServiceId).ToListAsync();
            db.RoomTypeServices.RemoveRange(await db.RoomTypeServices.Where(link => fixedServiceIds.Contains(link.ServiceId)).ToListAsync());
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(client, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/utility-readings", new
        {
            contractId = id, readingMonth = Month, electricEnd = 1248m, waterEnd = 49m
        })).StatusCode);
        var request = new { contractId = id, invoiceMonth = Month, dueDate = Today.AddDays(10) };
        var preview = await client.PostAsJsonAsync("/api/invoices/preview", request);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var expected = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(248_000m, expected.GetProperty("remainingPayable").GetDecimal());
        Assert.Equal(3_000_000m, expected.GetProperty("prepaidRentApplied").GetDecimal());
        var created = await client.PostAsJsonAsync("/api/invoices", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var actual = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected.GetProperty("total").GetDecimal(), actual.GetProperty("total").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/invoices", request)).StatusCode);
    }

    [Fact]
    public async Task Handover_after_start_is_valid_but_terminated_contract_or_revoked_manager_cannot_write()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await CreateAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Contracts.Where(contract => contract.ContractId == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(contract => contract.StartDate, Today.AddDays(-1)));
        }
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(client, id)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Contracts.Where(contract => contract.ContractId == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(contract => contract.Status, "Terminated"));
        }
        Assert.Equal(HttpStatusCode.Conflict, (await SaveAsync(client, id)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.PropertyManagerAssignments.Where(assignment => assignment.ManagerId == 7).ExecuteDeleteAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await SaveAsync(client, id)).StatusCode);
    }

    private static async Task<int> CreateAsync(HttpClient client)
    {
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        var response = await PostContractAsync(client, bookingId, Today, Today.AddYears(1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
    }
}
