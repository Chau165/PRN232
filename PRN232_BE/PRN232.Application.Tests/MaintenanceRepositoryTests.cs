using Microsoft.EntityFrameworkCore;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public class MaintenanceRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_filters_requests_through_contract_customer_id()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        context.Users.AddRange(
            new Users { UserId = 10, RoleId = 1, FullName = "Customer A", IsActive = true },
            new Users { UserId = 20, RoleId = 1, FullName = "Customer B", IsActive = true });
        context.Properties.Add(new Property { PropertyId = 1, OwnerId = 10 });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio" });
        context.Rooms.AddRange(
            new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A-1" },
            new Rooms { RoomId = 2, PropertyId = 1, RoomTypeId = 1, RoomCode = "A-2" });
        context.Contracts.AddRange(
            new Contracts
            {
                ContractId = 1, CustomerId = 10, RoomId = 1,
                StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2027, 1, 1)
            },
            new Contracts
            {
                ContractId = 2, CustomerId = 20, RoomId = 2,
                StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2027, 1, 1)
            });
        context.MaintenanceRequests.AddRange(
            new MaintenanceRequests { RequestId = 1, ContractId = 1, Description = "A", Status = "Pending", AssignmentNote = "Internal note" },
            new MaintenanceRequests { RequestId = 2, ContractId = 2, Description = "B", Status = "Pending", AssignmentNote = "Another internal note" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var results = await new MaintenanceRepository(context).GetAllAsync(10, null, null, null);

        var request = Assert.Single(results);
        Assert.Equal((1, 10, "Customer A"), (request.RequestId, request.CustomerId, request.CustomerName));
        Assert.Null(request.AssignmentNote);
        var customerDetail = await new MaintenanceRepository(context).GetByIdAsync(1, 10, null);
        Assert.Null(customerDetail!.AssignmentNote);
    }

    [Fact]
    public async Task GetAllAsync_filters_manager_requests_by_assigned_user()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        context.Users.AddRange(
            new Users { UserId = 10, RoleId = 1, FullName = "Manager A", IsActive = true },
            new Users { UserId = 20, RoleId = 1, FullName = "Manager B", IsActive = true },
            new Users { UserId = 30, RoleId = 2, FullName = "Customer", IsActive = true });
        context.Properties.AddRange(
            new Property { PropertyId = 1, OwnerId = 10 },
            new Property { PropertyId = 2, OwnerId = 20 });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio" });
        context.Rooms.AddRange(
            new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A-1" },
            new Rooms { RoomId = 2, PropertyId = 2, RoomTypeId = 1, RoomCode = "B-1" });
        context.Contracts.AddRange(
            new Contracts { ContractId = 1, CustomerId = 30, RoomId = 1 },
            new Contracts { ContractId = 2, CustomerId = 30, RoomId = 2 });
        context.MaintenanceRequests.AddRange(
            new MaintenanceRequests { RequestId = 1, ContractId = 1, AssignedTo = 10, Description = "A", Status = "Approved", AssignmentNote = "For manager A" },
            new MaintenanceRequests { RequestId = 2, ContractId = 2, AssignedTo = 20, Description = "B", Status = "Approved", AssignmentNote = "For manager B" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new MaintenanceRepository(context);
        var requestsWithBypassAttempt = await repository.GetAllAsync(null, 10, null, 20);
        Assert.Empty(requestsWithBypassAttempt);

        var results = await repository.GetAllAsync(null, 10, null, null);

        var request = Assert.Single(results);
        Assert.Equal(1, request.RequestId);
        Assert.Equal("For manager A", request.AssignmentNote);
        Assert.Equal("For manager A", (await repository.GetByIdAsync(1, null, 10))!.AssignmentNote);
    }
}
