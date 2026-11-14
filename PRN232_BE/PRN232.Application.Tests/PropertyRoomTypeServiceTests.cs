using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyRoomTypeServiceTests
{
    [Fact]
    public async Task CreateAsync_keeps_the_same_room_type_price_independent_per_property()
    {
        var repository = new FakePropertyRoomTypeRepository();
        var service = new PropertyRoomTypeService(repository, TimeProvider.System);

        var firstProperty = await service.CreateAsync(10, new CreatePropertyRoomTypeDto(3, 3_000_000m));
        var secondProperty = await service.CreateAsync(20, new CreatePropertyRoomTypeDto(3, 5_000_000m));

        Assert.Equal((10, 3, 3_000_000m), (firstProperty.PropertyId, firstProperty.RoomTypeId, firstProperty.MonthlyRent));
        Assert.Equal((20, 3, 5_000_000m), (secondProperty.PropertyId, secondProperty.RoomTypeId, secondProperty.MonthlyRent));
        Assert.Equal(2, repository.Items.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_rejects_non_positive_monthly_rent(decimal monthlyRent)
    {
        var repository = new FakePropertyRoomTypeRepository();

        await Assert.ThrowsAsync<PropertyRoomTypeValidationException>(() =>
            new PropertyRoomTypeService(repository, TimeProvider.System)
                .CreateAsync(10, new CreatePropertyRoomTypeDto(3, monthlyRent)));

        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_property_and_room_type_pair()
    {
        var repository = new FakePropertyRoomTypeRepository();
        var service = new PropertyRoomTypeService(repository, TimeProvider.System);
        await service.CreateAsync(10, new CreatePropertyRoomTypeDto(3, 3_000_000m));

        await Assert.ThrowsAsync<PropertyRoomTypeConflictException>(() =>
            service.CreateAsync(10, new CreatePropertyRoomTypeDto(3, 3_500_000m)));

        Assert.Single(repository.Items);
        Assert.Equal(3_000_000m, repository.Items[(10, 3)].MonthlyRent);
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_property_or_room_type()
    {
        var service = new PropertyRoomTypeService(new FakePropertyRoomTypeRepository(), TimeProvider.System);

        await Assert.ThrowsAsync<PropertyRoomTypeNotFoundException>(() =>
            service.CreateAsync(99, new CreatePropertyRoomTypeDto(3, 3_000_000m)));
        await Assert.ThrowsAsync<PropertyRoomTypeNotFoundException>(() =>
            service.CreateAsync(10, new CreatePropertyRoomTypeDto(99, 3_000_000m)));
    }

    [Fact]
    public async Task DeactivateAsync_keeps_configuration_but_prevents_new_room_use()
    {
        var repository = new FakePropertyRoomTypeRepository();
        var service = new PropertyRoomTypeService(repository, TimeProvider.System);
        await service.CreateAsync(10, new CreatePropertyRoomTypeDto(3, 3_000_000m));

        await service.UpdateStatusAsync(10, 3, new UpdatePropertyRoomTypeStatusDto(false));

        Assert.Single(repository.Items);
        Assert.False(await repository.IsActiveAsync(10, 3));
    }

    [Fact]
    public async Task Manager_can_read_only_the_property_with_an_active_assignment()
    {
        var repository = new FakePropertyRoomTypeRepository();
        var service = new PropertyRoomTypeService(repository, TimeProvider.System);
        await service.CreateAsync(10, new CreatePropertyRoomTypeDto(3, 3_000_000m));
        repository.ManagerAssignments.Add((10, 42));

        var roomTypes = await service.GetAllByPropertyIdAsync(10, 42, RoleNames.Manager);

        Assert.Single(roomTypes);
        await Assert.ThrowsAsync<PropertyRoomTypeAuthorizationException>(() =>
            service.GetAllByPropertyIdAsync(20, 42, RoleNames.Manager));
        repository.ManagerAssignments.Remove((10, 42));
        await Assert.ThrowsAsync<PropertyRoomTypeAuthorizationException>(() =>
            service.GetAllByPropertyIdAsync(10, 42, RoleNames.Manager));
    }

    [Fact]
    public async Task UpdateAsync_changes_only_the_selected_property_configuration()
    {
        var repository = new FakePropertyRoomTypeRepository();
        var service = new PropertyRoomTypeService(repository, TimeProvider.System);
        await service.CreateAsync(10, new CreatePropertyRoomTypeDto(3, 3_000_000m));
        await service.CreateAsync(20, new CreatePropertyRoomTypeDto(3, 5_000_000m));

        await service.UpdateAsync(10, 3, new UpdatePropertyRoomTypeDto(3_500_000m));

        Assert.Equal(3_500_000m, repository.Items[(10, 3)].MonthlyRent);
        Assert.Equal(5_000_000m, repository.Items[(20, 3)].MonthlyRent);
    }

    private sealed class FakePropertyRoomTypeRepository : IPropertyRoomTypeRepository
    {
        private readonly HashSet<int> _propertyIds = [10, 20];
        private readonly HashSet<int> _roomTypeIds = [3];

        public Dictionary<(int PropertyId, int RoomTypeId), PropertyRoomTypes> Items { get; } = [];
        public HashSet<(int PropertyId, int ManagerId)> ManagerAssignments { get; } = [];

        public Task<List<PropertyRoomTypeReadModel>> GetAllByPropertyIdAsync(int propertyId, DateTimeOffset asOfUtc) =>
            Task.FromResult(Items.Values
                .Where(item => item.PropertyId == propertyId)
                .Select(ToReadModel)
                .ToList());

        public Task<PropertyRoomTypeReadModel?> GetByIdAsync(int propertyId, int roomTypeId, DateTimeOffset asOfUtc) =>
            Task.FromResult(Items.TryGetValue((propertyId, roomTypeId), out var item) ? ToReadModel(item) : null);

        public Task<PropertyRoomTypes?> GetTrackedByIdAsync(int propertyId, int roomTypeId) =>
            Task.FromResult(Items.GetValueOrDefault((propertyId, roomTypeId)));

        public Task<bool> PropertyExistsAsync(int propertyId) => Task.FromResult(_propertyIds.Contains(propertyId));

        public Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId) =>
            Task.FromResult(ManagerAssignments.Contains((propertyId, managerId)));

        public Task<bool> RoomTypeExistsAsync(int roomTypeId) => Task.FromResult(_roomTypeIds.Contains(roomTypeId));

        public Task<bool> ExistsAsync(int propertyId, int roomTypeId) =>
            Task.FromResult(Items.ContainsKey((propertyId, roomTypeId)));

        public Task<bool> IsActiveAsync(int propertyId, int roomTypeId) =>
            Task.FromResult(Items.TryGetValue((propertyId, roomTypeId), out var item) && item.IsActive);

        public Task AddAsync(PropertyRoomTypes propertyRoomType)
        {
            Items.Add((propertyRoomType.PropertyId, propertyRoomType.RoomTypeId), propertyRoomType);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync() => Task.CompletedTask;

        private static PropertyRoomTypeReadModel ToReadModel(PropertyRoomTypes item) => new(
            item.PropertyId,
            item.RoomTypeId,
            "Premium",
            item.MonthlyRent,
            item.IsActive,
            0,
            0);
    }
}
