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
            new MaintenanceRequests { RequestId = 1, ContractId = 1, Description = "A", Status = "Pending" },
            new MaintenanceRequests { RequestId = 2, ContractId = 2, Description = "B", Status = "Pending" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var results = await new MaintenanceRepository(context).GetAllAsync(10, null, null);

        var request = Assert.Single(results);
        Assert.Equal((1, 10, "Customer A"), (request.RequestId, request.CustomerId, request.CustomerName));
    }
}
