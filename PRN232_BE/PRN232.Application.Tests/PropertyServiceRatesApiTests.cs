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

public class PropertyServiceRatesApiTests
{
    private static CreatePropertyServiceRateDto NewRate(int serviceId = 1) => new(
        serviceId, 4000, "kwh", "usage", new DateOnly(2027, 1, 1));

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_admin_can_create_rates(string? email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        if (email is not null) await LoginAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/properties/1/service-rates", NewRate());
        Assert.Equal(expected, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<AppDbContext>().PropertyServiceRates.CountAsync());
    }

    [Fact]
    public async Task Admin_creates_adjacent_rate_and_preserves_history_and_other_properties()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await context.PropertyServiceRates.SingleAsync(rate => rate.RateId == 1)).EffectiveTo = new DateOnly(2027, 1, 1);
            context.Properties.Add(new Property { PropertyId = 2, OwnerId = 8, PropertyName = "Other" });
            await context.SaveChangesAsync();
        }
        await LoginAsync(client, "admin@example.test");
        var response = await client.PostAsJsonAsync("/api/properties/1/service-rates", NewRate());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = (await response.Content.ReadFromJsonAsync<PropertyServiceRateDto>())!;
        Assert.True(created.RateId > 3);
        Assert.Equal(1, created.PropertyId);
        Assert.Equal(4000m, created.Price);
        Assert.Equal("Electricity - per kWh", created.ServiceName);
        var january = await client.GetFromJsonAsync<List<PropertyServiceRateDto>>("/api/properties/1/service-rates?month=2027-01-01");
        Assert.Equal(created.RateId, Assert.Single(january!, rate => rate.ServiceId == 1).RateId);
        // The same service/period is independent for a different property.
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/properties/2/service-rates", NewRate())).StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3500m, (await verify.PropertyServiceRates.SingleAsync(rate => rate.RateId == 1)).Price);
    }

    [Fact]
    public async Task Open_ended_and_duplicate_overlaps_are_rejected_without_changing_existing_rate()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        foreach (var input in new[]
        {
            NewRate(),
            NewRate() with { EffectiveFrom = new DateOnly(2000, 1, 1) },
            NewRate() with { EffectiveFrom = new DateOnly(1999, 1, 1), EffectiveTo = new DateOnly(2001, 1, 1) }
        })
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.PostAsJsonAsync("/api/properties/1/service-rates", input)).StatusCode);
        // An interval ending exactly when the existing interval starts is allowed.
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/properties/1/service-rates",
                NewRate() with { EffectiveFrom = new DateOnly(1999, 1, 1), EffectiveTo = new DateOnly(2000, 1, 1) })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var original = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PropertyServiceRates.SingleAsync(rate => rate.RateId == 1);
        Assert.Equal(3500m, original.Price);
        Assert.Null(original.EffectiveTo);
    }

    [Fact]
    public async Task Invalid_rate_inputs_are_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        var valid = NewRate();
        foreach (var input in new[]
        {
            valid with { ServiceId = 0 }, valid with { Price = -1 }, valid with { Price = 1.001m },
            valid with { Price = 10000000000000000m }, valid with { EffectiveFrom = default },
            valid with { EffectiveFrom = new DateOnly(2027, 1, 2) },
            valid with { EffectiveTo = new DateOnly(2027, 1, 1) },
            valid with { EffectiveTo = new DateOnly(2027, 2, 2) },
            valid with { UnitCode = "kwh", BillingBasis = "fixed_room" },
            valid with { UnitCode = null! }, valid with { BillingBasis = null! }
        })
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/properties/1/service-rates", input)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<AppDbContext>().PropertyServiceRates.CountAsync());
    }

    [Fact]
    public async Task Missing_property_or_service_returns_not_found()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync("/api/properties/999/service-rates", NewRate())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync("/api/properties/1/service-rates", NewRate(999))).StatusCode);
    }

    [Fact]
    public async Task Anonymous_cannot_read_property_rates()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/properties/1/service-rates")).StatusCode);
    }

    [Theory]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.OK)]
    [InlineData("admin@example.test", HttpStatusCode.OK)]
    public async Task Rates_follow_admin_and_property_owner_access(string email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, email);
        var response = await client.GetAsync("/api/properties/1/service-rates");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var rates = await response.Content.ReadFromJsonAsync<List<PropertyServiceRateDto>>();
            Assert.Equal(3, rates!.Count);
            Assert.All(rates, rate => Assert.Equal(1, rate.PropertyId));
            Assert.Equal("Electricity - per kWh", rates[0].ServiceName);
            Assert.Equal(3500m, rates[0].Price);
            Assert.Equal("kwh", rates[0].UnitCode);
            Assert.Equal("usage", rates[0].BillingBasis);
            Assert.Null(rates[0].EffectiveTo);
        }
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026-11-06")]
    public async Task Invalid_month_returns_validation_error(string month)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        var response = await client.GetAsync($"/api/properties/1/service-rates?month={month}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_property_is_not_confused_with_existing_property_without_rates()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Properties.Add(new Property { PropertyId = 2, OwnerId = 8, PropertyName = "Empty property" });
            await context.SaveChangesAsync();
        }
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/properties/999/service-rates")).StatusCode);
        var empty = await client.GetFromJsonAsync<List<PropertyServiceRateDto>>("/api/properties/2/service-rates");
        Assert.Empty(empty!);
        await LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/properties/2/service-rates")).StatusCode);
    }

    [Fact]
    public async Task Month_filter_respects_effective_boundaries_property_scope_and_does_not_write()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var oldRate = await context.PropertyServiceRates.SingleAsync(rate => rate.RateId == 1);
            oldRate.EffectiveTo = new DateOnly(2026, 11, 1);
            context.Properties.Add(new Property { PropertyId = 2, OwnerId = 8, PropertyName = "Other property" });
            context.PropertyServiceRates.AddRange(
                new PropertyServiceRates
                {
                    RateId = 4, PropertyId = 1, ServiceId = 1, Price = 4000,
                    UnitCode = "kwh", BillingBasis = "usage", EffectiveFrom = new DateOnly(2026, 11, 1),
                    EffectiveTo = new DateOnly(2026, 12, 1)
                },
                new PropertyServiceRates
                {
                    RateId = 5, PropertyId = 2, ServiceId = 1, Price = 9999,
                    UnitCode = "kwh", BillingBasis = "usage", EffectiveFrom = new DateOnly(2026, 11, 1)
                });
            await context.SaveChangesAsync();
        }
        await LoginAsync(client, "manager@example.test");
        var history = await client.GetFromJsonAsync<List<PropertyServiceRateDto>>("/api/properties/1/service-rates");
        Assert.Equal(new[] { 4, 1, 2, 3 }, history!.Select(rate => rate.RateId));
        var october = await client.GetFromJsonAsync<List<PropertyServiceRateDto>>(
            "/api/properties/1/service-rates?month=2026-10-01");
        Assert.Equal(3500m, Assert.Single(october!, rate => rate.ServiceId == 1).Price);
        var november = await client.GetFromJsonAsync<List<PropertyServiceRateDto>>(
            "/api/properties/1/service-rates?month=2026-11-01");
        Assert.Equal(3, november!.Count);
        Assert.Equal(4000m, Assert.Single(november, rate => rate.ServiceId == 1).Price);
        Assert.All(november, rate => Assert.Equal(1, rate.PropertyId));
        var december = await client.GetFromJsonAsync<List<PropertyServiceRateDto>>(
            "/api/properties/1/service-rates?month=2026-12-01");
        Assert.DoesNotContain(december!, rate => rate.ServiceId == 1);
        var beforeRates = await client.GetFromJsonAsync<List<PropertyServiceRateDto>>(
            "/api/properties/1/service-rates?month=1999-01-01");
        Assert.Empty(beforeRates!);
        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(5, await verifyContext.PropertyServiceRates.CountAsync());
        Assert.Equal(new DateOnly(2026, 11, 1),
            (await verifyContext.PropertyServiceRates.SingleAsync(rate => rate.RateId == 1)).EffectiveTo);
    }
}
