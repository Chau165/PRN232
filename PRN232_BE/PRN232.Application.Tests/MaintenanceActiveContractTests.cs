using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

// V4: maintenance requests only for an active contract, with a capped, non-blank description.
public sealed class MaintenanceActiveContractTests
{
    [Fact]
    public async Task Terminated_contract_cannot_receive_a_maintenance_request()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await SetContractStatusAsync(factory, 1, ContractStatuses.Terminated);
        await LoginAsync(client, "customer@example.test");

        using var response = await client.PostAsJsonAsync("/api/maintenance-requests", new
        {
            contractId = 1,
            description = "Vòi nước bị rò"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Hợp đồng đã kết thúc, không thể gửi yêu cầu sửa chữa.", await DetailAsync(response));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await context.MaintenanceRequests.AnyAsync(request => request.Description == "Vòi nước bị rò"));
    }

    [Fact]
    public async Task Active_contract_creates_the_request_with_a_trimmed_description()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        using var response = await client.PostAsJsonAsync("/api/maintenance-requests", new
        {
            contractId = 1,
            description = "  Vòi nước bị rò  "
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Vòi nước bị rò", body.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Description_longer_than_the_limit_is_refused()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        using var tooLong = await client.PostAsJsonAsync("/api/maintenance-requests", new
        {
            contractId = 1,
            description = new string('x', MaintenanceService.DescriptionMaxLength + 1)
        });
        using var atLimit = await client.PostAsJsonAsync("/api/maintenance-requests", new
        {
            contractId = 1,
            description = new string('x', MaintenanceService.DescriptionMaxLength)
        });

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("Mô tả sự cố tối đa 1000 ký tự.", await DetailAsync(tooLong));
        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);
    }

    [Fact]
    public async Task Blank_description_is_refused()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        using var response = await client.PostAsJsonAsync("/api/maintenance-requests", new
        {
            contractId = 1,
            description = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Vui lòng mô tả sự cố cần sửa chữa.", await DetailAsync(response));
    }

    private static async Task SetContractStatusAsync(MaintenanceApiFactory factory, int contractId, string status)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var contract = await context.Contracts.SingleAsync(c => c.ContractId == contractId);
        contract.Status = status;
        await context.SaveChangesAsync();
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = MaintenanceFlowApiTests.Password
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("detail").GetString();
    }
}
