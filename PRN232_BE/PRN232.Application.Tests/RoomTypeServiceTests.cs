using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class RoomTypeServiceTests
{
    [Fact]
    public async Task CreateAsync_normalizes_the_catalog_name_and_keeps_the_catalog_price_separate()
    {
        var repository = new FakeRoomTypeRepository();
        var service = new RoomTypeService(repository, new FakeServiceRepository());

        var created = await service.CreateAsync(new CreateRoomTypeDto("  Phòng   Deluxe  ", 0m));

        Assert.Equal("Phòng Deluxe", created.TypeName);
        Assert.Equal(0m, created.Price);
        Assert.Single(repository.Items);
        Assert.Equal("Phòng Deluxe", repository.Items[0].TypeName);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_name_regardless_of_case_and_spacing()
    {
        var repository = new FakeRoomTypeRepository();
        repository.Items.Add(new RoomTypes
        {
            RoomTypeId = 1,
            TypeName = "Studio",
            Price = 0m
        });
        var service = new RoomTypeService(repository, new FakeServiceRepository());

        await Assert.ThrowsAsync<RoomTypeConflictException>(() =>
            service.CreateAsync(new CreateRoomTypeDto("  sTuDiO  ", 0m)));

        Assert.Single(repository.Items);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_rejects_a_blank_catalog_name(string typeName)
    {
        var repository = new FakeRoomTypeRepository();
        var service = new RoomTypeService(repository, new FakeServiceRepository());

        await Assert.ThrowsAsync<RoomTypeValidationException>(() =>
            service.CreateAsync(new CreateRoomTypeDto(typeName, 0m)));

        Assert.Empty(repository.Items);
    }

    private sealed class FakeRoomTypeRepository : IRoomTypeRepository
    {
        public List<RoomTypes> Items { get; } = [];

        public Task<List<RoomTypes>> GetAllAsync() =>
            Task.FromResult(Items.ToList());

        public Task<RoomTypes?> GetByIdAsync(int id) =>
            Task.FromResult(Items.FirstOrDefault(item => item.RoomTypeId == id));

        public Task<RoomTypes?> GetTrackedByIdAsync(int id) => GetByIdAsync(id);

        public Task<bool> ExistsByNameAsync(string typeName) =>
            Task.FromResult(Items.Any(item =>
                string.Equals(item.TypeName, typeName, StringComparison.OrdinalIgnoreCase)));

        public Task<RoomTypes> AddAsync(RoomTypes roomType)
        {
            roomType.RoomTypeId = Items.Count + 1;
            Items.Add(roomType);
            return Task.FromResult(roomType);
        }

        public void Remove(RoomTypes roomType) => Items.Remove(roomType);

        public Task<bool> HasRoomsAsync(int roomTypeId) => Task.FromResult(false);

        public Task SaveChangesAsync() => Task.CompletedTask;

        public Task<List<Service>> GetServicesAsync(int roomTypeId) =>
            Task.FromResult(new List<Service>());

        public Task<bool> ServiceLinkExistsAsync(int roomTypeId, int serviceId) =>
            Task.FromResult(false);

        public Task AddServiceLinkAsync(int roomTypeId, int serviceId) =>
            Task.CompletedTask;

        public Task<bool> RemoveServiceLinkAsync(int roomTypeId, int serviceId) =>
            Task.FromResult(false);
    }

    private sealed class FakeServiceRepository : IServiceRepository
    {
        public Task<List<Service>> GetAllAsync() =>
            Task.FromResult(new List<Service>());

        public Task<Service?> GetByIdAsync(int id) => Task.FromResult<Service?>(null);

        public Task<Service?> GetTrackedByIdAsync(int id) => Task.FromResult<Service?>(null);

        public Task<Service> AddAsync(Service service) => Task.FromResult(service);

        public void Remove(Service service)
        {
        }

        public Task SaveChangesAsync() => Task.CompletedTask;
    }
}
