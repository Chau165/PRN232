using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyManagerAssignmentsApiTests
{
    [Fact]
    public async Task Admin_can_assign_manager_and_duplicate_assignment_returns_conflict()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        using var createdResponse = await client.PostAsJsonAsync(
            "/api/properties/1/managers",
            new { managerId = 8 });

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var createdAssignment = await createdResponse.Content.ReadFromJsonAsync<PropertyManagerAssignmentDto>();
        Assert.NotNull(createdAssignment);
        Assert.Equal(1, createdAssignment.PropertyId);
        Assert.Equal(8, createdAssignment.ManagerId);
        Assert.True(createdAssignment.ManagerIsActive);

        var assignments = await client.GetFromJsonAsync<PropertyManagerAssignmentDto[]>(
            "/api/properties/1/managers");
        Assert.Equal([7, 8], assignments!.Select(assignment => assignment.ManagerId).Order());

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/properties/1/managers",
            new { managerId = 8 });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(9, (await context.Properties.SingleAsync(property => property.PropertyId == 1)).OwnerId);
    }

    [Fact]
    public async Task Admin_reads_assignments_for_many_properties_grouped_by_property()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Properties.AddRange(
                new Property { PropertyId = 2, OwnerId = 9, PropertyName = "Second" },
                new Property { PropertyId = 3, OwnerId = 9, PropertyName = "Third" });
            context.PropertyManagerAssignments.AddRange(
                new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 },
                new PropertyManagerAssignment { PropertyId = 2, ManagerId = 8 },
                new PropertyManagerAssignment { PropertyId = 3, ManagerId = 7 });
            await context.SaveChangesAsync();
        }
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        var filtered = await client.GetFromJsonAsync<PropertyManagersDto[]>(
            "/api/properties/managers?propertyIds=1,2");
        Assert.NotNull(filtered);
        Assert.Equal([1, 2], filtered.Select(group => group.PropertyId));
        Assert.Equal([7, 8], filtered[0].Managers.Select(manager => manager.ManagerId).Order());
        Assert.Equal([8], filtered[1].Managers.Select(manager => manager.ManagerId));
        Assert.All(filtered, group => Assert.All(group.Managers,
            manager => Assert.Equal(group.PropertyId, manager.PropertyId)));

        var all = await client.GetFromJsonAsync<PropertyManagersDto[]>("/api/properties/managers");
        Assert.Equal([1, 2, 3], all!.Select(group => group.PropertyId));

        using var invalid = await client.GetAsync("/api/properties/managers?propertyIds=1,abc");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Theory]
    [InlineData("manager@example.test")]
    [InlineData("customer@example.test")]
    public async Task Non_admin_cannot_read_batch_assignments(string email)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, email);

        using var response = await client.GetAsync("/api/managers/properties?managerIds=7");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await Non_admin_cannot_read_batch_property_assignments(client);
    }

    [Fact]
    public async Task Admin_reads_properties_for_many_managers_grouped_by_manager()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Properties.AddRange(
                new Property { PropertyId = 2, OwnerId = 9, PropertyName = "Second" },
                new Property { PropertyId = 3, OwnerId = 9, PropertyName = "Third" });
            context.PropertyManagerAssignments.AddRange(
                new PropertyManagerAssignment { PropertyId = 2, ManagerId = 7 },
                new PropertyManagerAssignment { PropertyId = 3, ManagerId = 8 });
            await context.SaveChangesAsync();
        }
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        var filtered = await client.GetFromJsonAsync<ManagerPropertiesDto[]>(
            "/api/managers/properties?managerIds=7,8");
        Assert.NotNull(filtered);
        Assert.Equal([7, 8], filtered.Select(group => group.ManagerId));
        Assert.Equal([1, 2], filtered[0].Properties.Select(property => property.PropertyId));
        Assert.Equal([3], filtered[1].Properties.Select(property => property.PropertyId));
        Assert.Equal(2, filtered[0].Properties[0].RoomCount);

        var onlyEight = await client.GetFromJsonAsync<ManagerPropertiesDto[]>(
            "/api/managers/properties?managerIds=8");
        Assert.Equal([8], onlyEight!.Select(group => group.ManagerId));

        var all = await client.GetFromJsonAsync<ManagerPropertiesDto[]>("/api/managers/properties");
        Assert.Equal([7, 8], all!.Select(group => group.ManagerId));

        using var invalid = await client.GetAsync("/api/managers/properties?managerIds=7,-1");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static async Task Non_admin_cannot_read_batch_property_assignments(HttpClient client)
    {
        using var response = await client.GetAsync("/api/properties/managers?propertyIds=1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("manager@example.test")]
    [InlineData("customer@example.test")]
    public async Task Non_admin_cannot_assign_a_manager(string email)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, email);

        using var response = await client.PostAsJsonAsync(
            "/api/properties/1/managers",
            new { managerId = 8 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_who_does_not_own_property_cannot_assign_manager()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "admin-b@example.test");

        using var response = await client.PostAsJsonAsync(
            "/api/properties/1/managers",
            new { managerId = 8 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await context.PropertyManagerAssignments.AnyAsync(
            assignment => assignment.PropertyId == 1 && assignment.ManagerId == 8));
    }

    [Fact]
    public async Task Admin_cannot_assign_inactive_manager()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        using var response = await client.PostAsJsonAsync(
            "/api/properties/1/managers",
            new { managerId = 14 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await context.PropertyManagerAssignments.AnyAsync(
            assignment => assignment.PropertyId == 1 && assignment.ManagerId == 14));
    }

    [Fact]
    public async Task Assignment_request_rejects_non_positive_manager_id()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        using var response = await client.PostAsJsonAsync(
            "/api/properties/1/managers",
            new { managerId = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Legacy_owner_id_that_points_to_manager_returns_actionable_forbidden_response()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Properties.Add(new Property
            {
                PropertyId = 2,
                OwnerId = 7,
                PropertyName = "Legacy property"
            });
            await context.SaveChangesAsync();
        }

        await BookingsApiTests.LoginAsync(client, "admin@example.test");
        using var response = await client.PostAsJsonAsync(
            "/api/properties/2/managers",
            new { managerId = 8 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var detail = body.RootElement.GetProperty("detail").GetString();
        Assert.Contains("OwnerId=7", detail);
        Assert.Contains("Manager A", detail);
        Assert.Contains("không bị thay đổi", detail);

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(7, (await verificationContext.Properties.SingleAsync(
            property => property.PropertyId == 2)).OwnerId);
        Assert.False(await verificationContext.PropertyManagerAssignments.AnyAsync(
            assignment => assignment.PropertyId == 2));
    }

    [Fact]
    public async Task Manager_reads_only_assigned_properties_and_cannot_read_another_managers_scope()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Properties.AddRange(
                new Property { PropertyId = 2, OwnerId = 9, PropertyName = "Assigned two" },
                new Property { PropertyId = 3, OwnerId = 9, PropertyName = "Unassigned" });
            context.PropertyManagerAssignments.AddRange(
                new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 },
                new PropertyManagerAssignment { PropertyId = 2, ManagerId = 8 });
            await context.SaveChangesAsync();
        }

        await BookingsApiTests.LoginAsync(client, "manager-b@example.test");
        var managedProperties = await client.GetFromJsonAsync<PropertyDto[]>(
            "/api/properties/my-managed");
        Assert.Equal([1, 2], managedProperties!.Select(property => property.PropertyId).Order());

        using var unassignedResponse = await client.GetAsync("/api/properties/3");
        using var otherManagerResponse = await client.GetAsync("/api/managers/7/properties");
        Assert.Equal(HttpStatusCode.Forbidden, unassignedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherManagerResponse.StatusCode);
    }

    [Fact]
    public async Task Newly_assigned_manager_sees_the_property_through_my_managed_endpoint()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "admin@example.test");
        using var assignmentResponse = await client.PostAsJsonAsync(
            "/api/properties/1/managers",
            new { managerId = 8 });
        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        await BookingsApiTests.LoginAsync(client, "manager-b@example.test");
        var managedProperties = await client.GetFromJsonAsync<PropertyDto[]>(
            "/api/properties/my-managed");

        Assert.Contains(managedProperties!, property => property.PropertyId == 1);
    }

    [Fact]
    public async Task Revoke_removes_only_selected_assignment_and_preserves_owner_and_other_manager()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "admin@example.test");
        using var assignResponse = await client.PostAsJsonAsync(
            "/api/properties/1/managers",
            new { managerId = 8 });
        Assert.Equal(HttpStatusCode.Created, assignResponse.StatusCode);

        using var revokeResponse = await client.DeleteAsync("/api/properties/1/managers/7");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
        var assignments = await client.GetFromJsonAsync<PropertyManagerAssignmentDto[]>(
            "/api/properties/1/managers");
        Assert.Equal([8], assignments!.Select(assignment => assignment.ManagerId));
        var managerProperties = await client.GetFromJsonAsync<PropertyDto[]>(
            "/api/managers/7/properties");
        Assert.Empty(managerProperties!);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(9, (await context.Properties.SingleAsync(property => property.PropertyId == 1)).OwnerId);
        Assert.True(await context.Users.AnyAsync(user => user.UserId == 7));
    }

    [Fact]
    public async Task Revoke_is_blocked_while_manager_has_an_active_contract_to_handoff()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Contracts.Add(new Contracts
            {
                ContractId = 1,
                RoomId = 1,
                CustomerId = 10,
                ProcessedByStaffId = 7,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2027, 1, 1),
                MonthlyRent = 3_000_000m,
                DepositAmount = 3_000_000m,
                Status = "Active"
            });
            await context.SaveChangesAsync();
        }

        await BookingsApiTests.LoginAsync(client, "admin@example.test");
        using var response = await client.DeleteAsync("/api/properties/1/managers/7");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("hợp đồng đang hiệu lực", body.RootElement.GetProperty("detail").GetString());
        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await verificationContext.PropertyManagerAssignments.AnyAsync(
            assignment => assignment.PropertyId == 1 && assignment.ManagerId == 7));
    }
}
