using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class AmenityServiceTests
{
    [Fact]
    public async Task GetAll_maps_repository_entities_to_response_dtos()
    {
        var repository = new AmenityRepositoryStub(
            new Amenities { AmenityId = 2, Name = "Balcony" },
            new Amenities { AmenityId = 1, Name = "Wi-Fi" });
        var service = new AmenityService(repository);

        var result = await service.GetAllAsync();

        Assert.Equal(
            [new AmenityDto(2, "Balcony"), new AmenityDto(1, "Wi-Fi")],
            result);
    }

    [Fact]
    public async Task GetById_maps_entity_and_preserves_missing_result()
    {
        var repository = new AmenityRepositoryStub(new Amenities { AmenityId = 7, Name = "Elevator" });
        var service = new AmenityService(repository);

        Assert.Equal(new AmenityDto(7, "Elevator"), await service.GetByIdAsync(7));
        Assert.Null(await service.GetByIdAsync(404));
    }

    [Fact]
    public async Task Create_trims_name_and_maps_saved_entity_to_response()
    {
        var repository = new AmenityRepositoryStub();
        var service = new AmenityService(repository);

        var result = await service.CreateAsync(new CreateAmenityDto("  Laundry room  "));

        Assert.Equal(new AmenityDto(1, "Laundry room"), result);
        Assert.Equal("Laundry room", repository.AddedEntity?.Name);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_empty_name_without_writing(string? name)
    {
        var repository = new AmenityRepositoryStub();
        var service = new AmenityService(repository);

        await Assert.ThrowsAsync<ReferenceDataValidationException>(
            () => service.CreateAsync(new CreateAmenityDto(name!)));

        Assert.Null(repository.AddedEntity);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_changes_only_name_and_maps_the_tracked_entity()
    {
        var roomLinks = new List<RoomAmenities> { new() { RoomId = 11, AmenityId = 5 } };
        var propertyLinks = new List<PropertyAmenities> { new() { PropertyId = 13, AmenityId = 5 } };
        var entity = new Amenities
        {
            AmenityId = 5,
            Name = "Old name",
            RoomAmenities = roomLinks,
            PropertyAmenities = propertyLinks
        };
        var repository = new AmenityRepositoryStub(entity);
        var service = new AmenityService(repository);

        var result = await service.UpdateAsync(5, new UpdateAmenityDto("  New name  "));

        Assert.Equal(new AmenityDto(5, "New name"), result);
        Assert.Equal(5, entity.AmenityId);
        Assert.Equal("New name", entity.Name);
        Assert.Same(roomLinks, entity.RoomAmenities);
        Assert.Same(propertyLinks, entity.PropertyAmenities);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_returns_not_found_before_validating_when_entity_is_missing()
    {
        var repository = new AmenityRepositoryStub();
        var service = new AmenityService(repository);

        var result = await service.UpdateAsync(404, new UpdateAmenityDto(" "));

        Assert.Null(result);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    private sealed class AmenityRepositoryStub(params Amenities[] amenities) : IAmenityRepository
    {
        private readonly List<Amenities> _amenities = amenities.ToList();

        public Amenities? AddedEntity
        {
            get; private set;
        }

        public int SaveChangesCallCount
        {
            get; private set;
        }

        public Task<List<Amenities>> GetAllAsync() => Task.FromResult(_amenities.ToList());

        public Task<Amenities?> GetByIdAsync(int id) =>
            Task.FromResult(_amenities.FirstOrDefault(amenity => amenity.AmenityId == id));

        public Task<Amenities?> GetTrackedByIdAsync(int id) =>
            Task.FromResult(_amenities.FirstOrDefault(amenity => amenity.AmenityId == id));

        public Task<Amenities> AddAsync(Amenities amenity)
        {
            amenity.AmenityId = _amenities.Count + 1;
            _amenities.Add(amenity);
            AddedEntity = amenity;
            return Task.FromResult(amenity);
        }

        public void Remove(Amenities amenity) => _amenities.Remove(amenity);

        public Task SaveChangesAsync()
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }
}
