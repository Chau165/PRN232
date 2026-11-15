using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.AdministrativeDivisions;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyCreationTests
{
    [Fact]
    public async Task CreateProperty_stores_supplied_coordinates_and_uploaded_image_url()
    {
        await using var context = await CreateContextAsync();
        var storage = new FakePropertyImageStorage();
        var service = new PropertyService(
            new PropertyRepository(context),
            new AdministrativeDivisionCatalog(),
            storage);

        var created = await service.CreateAsync(
            new CreatePropertyDto(
                "Nhà trọ Bình An",
                "12 Đường số 5",
                "01",
                "00004",
                "Mô tả", Latitude: 21.032m, Longitude: 105.814m),
            ownerId: 10,
            isAdmin: true,
            new PropertyImageInput("property.png", "image/png", ValidPng()));

        Assert.Equal(21.032m, created.Latitude);
        Assert.Equal(105.814m, created.Longitude);
        Assert.Equal("https://cdn.example.test/properties/property.png", created.PrimaryImageUrl);
        Assert.Equal("Phường Ba Đình", created.Ward);
        Assert.Equal(string.Empty, created.District);
        Assert.Equal("Thành phố Hà Nội", created.City);
        Assert.Single(storage.UploadedImages);

        var persisted = await context.Properties.SingleAsync(property => property.PropertyId == created.PropertyId);
        Assert.Equal(21.032m, persisted.Latitude);
        Assert.Equal(105.814m, persisted.Longitude);
        Assert.Equal(created.PrimaryImageUrl, persisted.PrimaryImageUrl);
    }

    [Fact]
    public async Task CreateProperty_rejects_missing_coordinates_before_upload()
    {
        await using var context = await CreateContextAsync();
        var storage = new FakePropertyImageStorage();
        var service = new PropertyService(
            new PropertyRepository(context),
            new AdministrativeDivisionCatalog(),
            storage);

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto("Nhà trọ lỗi", "Địa chỉ không tồn tại", "01", "00004"),
            ownerId: 10,
            isAdmin: true,
            new PropertyImageInput("property.png", "image/png", ValidPng())));

        Assert.Empty(storage.UploadedImages);
        Assert.Empty(context.Properties.Where(property => property.PropertyName == "Nhà trọ lỗi"));
    }

    [Fact]
    public async Task UpdateProperty_stores_changed_pin_and_keeps_existing_image_without_new_file()
    {
        await using var context = await CreateContextAsync();
        context.Properties.Add(new Property
        {
            PropertyId = 21,
            OwnerId = 10,
            PropertyName = "Nhà trọ cũ",
            AddressText = "Địa chỉ cũ",
            Ward = "Phường cũ",
            District = "Quận cũ",
            City = "Thành phố cũ",
            Latitude = 0,
            Longitude = 0,
            PrimaryImageUrl = "https://cdn.example.test/properties/old.png"
        });
        await context.SaveChangesAsync();

        var storage = new FakePropertyImageStorage();
        var service = new PropertyService(
            new PropertyRepository(context),
            new AdministrativeDivisionCatalog(),
            storage);

        var updated = await service.UpdateAsync(
            21,
            new UpdatePropertyDto(
                "Nhà trọ mới",
                "12 Đường số 5",
                "01",
                "00004", Latitude:21.032m, Longitude:105.814m),
            requestingUserId: 10,
            isAdmin: true);

        Assert.NotNull(updated);
        Assert.Equal(21.032m, updated!.Latitude);
        Assert.Equal(105.814m, updated.Longitude);
        Assert.Equal("https://cdn.example.test/properties/old.png", updated.PrimaryImageUrl);
        Assert.Empty(storage.UploadedImages);
    }

    [Fact]
    public async Task CreateProperty_rejects_a_spoofed_image_before_storage_upload()
    {
        await using var context = await CreateContextAsync();
        var storage = new FakePropertyImageStorage();
        var service = new PropertyService(
            new PropertyRepository(context),
            new AdministrativeDivisionCatalog(),
            storage);

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto("Nhà trọ", "Địa chỉ", "01", "00004", Latitude:21m, Longitude:105m),
            ownerId: 10,
            isAdmin: true,
            new PropertyImageInput("property.png", "image/png", [1, 2, 3])));

        Assert.Empty(storage.UploadedImages);
    }

    private static byte[] ValidPng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0, 0, 0, 0, 0, 0, 0, 0,
        0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    // Rebuild 6.4: deleting a property also removes its image from Storage — only after the row is gone, and a Storage
    // failure never undoes or fails the delete.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteProperty_removes_its_image_from_storage_after_the_row_is_deleted(bool storageFails)
    {
        await using var context = await CreateContextAsync();
        var storage = new FakePropertyImageStorage { FailDelete = storageFails };
        var service = new PropertyService(new PropertyRepository(context), new AdministrativeDivisionCatalog(), storage);
        var created = await service.CreateAsync(new CreatePropertyDto("Nhà trọ Bình An", "12 Đường số 5", "01", "00004", "Mô tả", Latitude: 21.032m, Longitude: 105.814m),
            ownerId: 10, isAdmin: true, new PropertyImageInput("property.png", "image/png", ValidPng()));

        Assert.True(await service.DeleteAsync(created.PropertyId, requestingUserId: 10, isAdmin: true));

        Assert.False(await context.Properties.AnyAsync(property => property.PropertyId == created.PropertyId));
        Assert.Equal(storageFails ? [] : ["https://cdn.example.test/properties/property.png"], storage.DeletedImageUrls);
    }

    [Fact]
    public async Task DeleteProperty_with_rooms_keeps_both_the_row_and_its_image()
    {
        await using var context = await CreateContextAsync();
        var storage = new FakePropertyImageStorage();
        var service = new PropertyService(new PropertyRepository(context), new AdministrativeDivisionCatalog(), storage);
        var created = await service.CreateAsync(new CreatePropertyDto("Nhà trọ Bình An", "12 Đường số 5", "01", "00004", "Mô tả", Latitude: 21.032m, Longitude: 105.814m),
            ownerId: 10, isAdmin: true, new PropertyImageInput("property.png", "image/png", ValidPng()));
        context.Rooms.Add(new Rooms { RoomId = 1, PropertyId = created.PropertyId, RoomTypeId = 1, RoomCode = "A101", Status = "Available" });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<PropertyHasRoomsException>(() => service.DeleteAsync(created.PropertyId, 10, isAdmin: true));

        Assert.True(await context.Properties.AnyAsync(property => property.PropertyId == created.PropertyId));
        Assert.Empty(storage.DeletedImageUrls);
    }

    private static async Task<AppDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"property-create-{Guid.NewGuid()}")
            .Options;
        var context = new AppDbContext(options);
        context.Users.Add(new Users
        {
            UserId = 10,
            RoleId = 1,
            FullName = "Admin",
            Email = "admin@example.test",
            IsActive = true
        });
        await context.SaveChangesAsync();
        return context;
    }

    private sealed class FakePropertyImageStorage : IPropertyImageStorage
    {
        public List<PropertyImageInput> UploadedImages { get; } = [];

        public Task<string> UploadAsync(PropertyImageInput image, CancellationToken cancellationToken = default)
        {
            UploadedImages.Add(image);
            return Task.FromResult("https://cdn.example.test/properties/property.png");
        }

        public List<string> DeletedImageUrls { get; } = [];
        public bool FailDelete { get; init; }

        public Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            if (FailDelete)
            {
                throw new PropertyImageStorageException();
            }
            DeletedImageUrls.Add(imageUrl);
            return Task.CompletedTask;
        }
    }
}
