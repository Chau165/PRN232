using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PRN232.Api.Controllers;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class AmenitiesApiContractTests
{
    [Fact]
    public async Task Get_all_keeps_route_status_and_camel_case_dto_contract()
    {
        await using var api = await CreateApiAsync();
        using var client = api.CreateClient();
        await SeedAmenitiesAsync(api, new Amenities { Name = "Wi-Fi" }, new Amenities { Name = "Balcony" });

        using var response = await client.GetAsync("/api/amenities");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(["Balcony", "Wi-Fi"], items.Select(item => item.GetProperty("name").GetString()));
        Assert.All(items, item =>
        {
            Assert.Equal(["amenityId", "name"], item.EnumerateObject().Select(property => property.Name));
        });
    }

    [Fact]
    public async Task Get_by_id_keeps_response_and_not_found_contract()
    {
        await using var api = await CreateApiAsync();
        using var client = api.CreateClient();
        var amenityId = await SeedAmenitiesAsync(api, new Amenities { Name = "Elevator" });

        using var found = await client.GetAsync($"/api/amenities/{amenityId}");
        using var foundDocument = JsonDocument.Parse(await found.Content.ReadAsStringAsync());
        using var missing = await client.GetAsync("/api/amenities/404");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(amenityId, foundDocument.RootElement.GetProperty("amenityId").GetInt32());
        Assert.Equal("Elevator", foundDocument.RootElement.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Create_keeps_authorization_validation_and_created_response_contract()
    {
        await using var api = await CreateApiAsync();
        using var client = api.CreateClient();

        using var anonymous = await client.PostAsJsonAsync("/api/amenities", new
        {
            name = "Gym"
        });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");
        using var customer = await client.PostAsJsonAsync("/api/amenities", new
        {
            name = "Gym"
        });
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
        using var created = await client.PostAsJsonAsync("/api/amenities", new
        {
            name = "  Gym  "
        });
        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var amenityId = createdDocument.RootElement.GetProperty("amenityId").GetInt32();
        using var invalid = await client.PostAsJsonAsync("/api/amenities", new
        {
            name = " "
        });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains($"/api/amenities/{amenityId}", created.Headers.Location!.ToString().ToLowerInvariant());
        Assert.Equal("Gym", createdDocument.RootElement.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(1, await CountAmenitiesAsync(api));
    }

    [Fact]
    public async Task Update_and_delete_keep_statuses_and_persist_entity_changes()
    {
        await using var api = await CreateApiAsync();
        using var client = api.CreateClient();
        var amenityId = await SeedAmenitiesAsync(api, new Amenities { Name = "Old name" });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        using var updated = await client.PutAsJsonAsync($"/api/amenities/{amenityId}", new
        {
            name = "  New name  "
        });
        using var updatedDocument = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        using var invalidUpdate = await client.PutAsJsonAsync($"/api/amenities/{amenityId}", new
        {
            name = " "
        });
        using var missingUpdate = await client.PutAsJsonAsync("/api/amenities/404", new
        {
            name = " "
        });
        using var deleted = await client.DeleteAsync($"/api/amenities/{amenityId}");
        using var missingDelete = await client.DeleteAsync($"/api/amenities/{amenityId}");

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(amenityId, updatedDocument.RootElement.GetProperty("amenityId").GetInt32());
        Assert.Equal("New name", updatedDocument.RootElement.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingDelete.StatusCode);
        Assert.Equal(0, await CountAmenitiesAsync(api));
    }

    private static async Task<TestApi> CreateApiAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddScoped<IAmenityRepository, AmenityRepository>();
        builder.Services.AddScoped<IAmenityService, AmenityService>();
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AmenitiesController).Assembly);

        var application = builder.Build();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync();

        using var scope = application.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        return new TestApi(application, connection);
    }

    private static async Task<int> SeedAmenitiesAsync(TestApi api, params Amenities[] amenities)
    {
        using var scope = api.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Amenities.AddRange(amenities);
        await context.SaveChangesAsync();
        return amenities.FirstOrDefault()?.AmenityId ?? 0;
    }

    private static async Task<int> CountAmenitiesAsync(TestApi api)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Amenities.CountAsync();
    }

    private sealed class TestApi(WebApplication application, SqliteConnection connection) : IAsyncDisposable
    {
        public IServiceProvider Services => application.Services;

        public HttpClient CreateClient() => application.GetTestClient();

        public async ValueTask DisposeAsync()
        {
            await application.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, role)],
                Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
