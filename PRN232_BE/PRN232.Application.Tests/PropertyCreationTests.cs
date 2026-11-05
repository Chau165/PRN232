using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyCreationTests
{
    [Fact]
    public async Task CreateProperty_geocodes_address_and_stores_uploaded_image_url()
    {
        await using var context = await CreateContextAsync();
        var geocoder = new FakeGeocodingService(new AddressCoordinates(10.123456m, 106.654321m));
        var storage = new FakePropertyImageStorage();
        var service = new PropertyService(new PropertyRepository(context), geocoder, storage);

        var created = await service.CreateAsync(
            new CreatePropertyDto("Nhà trọ Bình An", "12 Đường số 5", "Phường 1", "Thủ Đức", "TP. Hồ Chí Minh", "Mô tả"),
            ownerId: 10,
            isAdmin: true,
            new PropertyImageInput("property.png", "image/png", ValidPng()));

        Assert.Equal(10.123456m, created.Latitude);
        Assert.Equal(106.654321m, created.Longitude);
        Assert.Equal("https://cdn.example.test/properties/property.png", created.PrimaryImageUrl);
        Assert.Equal("12 Đường số 5, Phường 1, Thủ Đức, TP. Hồ Chí Minh", geocoder.LastQuery);
        Assert.Single(storage.UploadedImages);

        var persisted = await context.Properties.SingleAsync(property => property.PropertyId == created.PropertyId);
        Assert.Equal(10.123456m, persisted.Latitude);
        Assert.Equal(106.654321m, persisted.Longitude);
        Assert.Equal(created.PrimaryImageUrl, persisted.PrimaryImageUrl);
    }

    [Fact]
    public async Task CreateProperty_rejects_an_address_that_cannot_be_geocoded()
    {
        await using var context = await CreateContextAsync();
        var geocoder = new FakeGeocodingService(null);
        var storage = new FakePropertyImageStorage();
        var service = new PropertyService(new PropertyRepository(context), geocoder, storage);

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto("Nhà trọ lỗi", "Địa chỉ không tồn tại", "", "", "", null),
            ownerId: 10,
            isAdmin: true,
            new PropertyImageInput("property.png", "image/png", ValidPng())));

        Assert.Empty(storage.UploadedImages);
        Assert.Empty(context.Properties.Where(property => property.PropertyName == "Nhà trọ lỗi"));
    }

    [Fact]
    public async Task UpdateProperty_regeocodes_address_and_keeps_existing_image_without_new_file()
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

        var geocoder = new FakeGeocodingService(new AddressCoordinates(10.7m, 106.8m));
        var storage = new FakePropertyImageStorage();
        var service = new PropertyService(new PropertyRepository(context), geocoder, storage);

        var updated = await service.UpdateAsync(
            21,
            new UpdatePropertyDto("Nhà trọ mới", "Địa chỉ mới", "Phường mới", "Quận mới", "Thành phố mới", null),
            requestingUserId: 10,
            isAdmin: true);

        Assert.NotNull(updated);
        Assert.Equal(10.7m, updated!.Latitude);
        Assert.Equal(106.8m, updated.Longitude);
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
            new FakeGeocodingService(new AddressCoordinates(10m, 106m)),
            storage);

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto("Nhà trọ", "Địa chỉ", "", "", "", null),
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

    private sealed class FakeGeocodingService(AddressCoordinates? result) : IPropertyGeocodingService
    {
        private readonly AddressCoordinates? _result = result;

        public string? LastQuery { get; private set; }

        public Task<AddressCoordinates?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
        {
            LastQuery = address;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakePropertyImageStorage : IPropertyImageStorage
    {
        public List<PropertyImageInput> UploadedImages { get; } = [];

        public Task<string> UploadAsync(PropertyImageInput image, CancellationToken cancellationToken = default)
        {
            UploadedImages.Add(image);
            return Task.FromResult("https://cdn.example.test/properties/property.png");
        }

        public Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
