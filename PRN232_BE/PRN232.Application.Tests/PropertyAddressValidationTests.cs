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

public sealed class PropertyAddressValidationTests
{
    [Fact]
    public async Task Create_resolves_official_names_and_geocodes_the_canonical_address()
    {
        await using var context = await CreateContextAsync();
        var geocoder = new FixedGeocoder(new AddressGeocodingCandidate(
            new AddressCoordinates(21.032m, 105.814m),
            "12 đường Lê Lợi, Phường Ba Đình, Thành phố Hà Nội, Việt Nam",
            "Nominatim",
            "house",
            "12",
            "đường Lê Lợi",
            "Phường Ba Đình",
            "Thành phố Hà Nội",
            "vn"));
        var service = new PropertyService(
            new PropertyRepository(context),
            new AdministrativeDivisionCatalog(),
            geocoder);

        var created = await service.CreateAsync(
            new CreatePropertyDto(
                "Nhà trọ Bình An",
                "12 đường Lê Lợi",
                "01",
                "00004"),
            ownerId: 10,
            isAdmin: true);

        Assert.Equal("12 đường Lê Lợi", created.AddressText);
        Assert.Equal("Phường Ba Đình", created.Ward);
        Assert.Equal(string.Empty, created.District);
        Assert.Equal("Thành phố Hà Nội", created.City);
        Assert.Equal(21.032m, created.Latitude);
        Assert.Equal(105.814m, created.Longitude);
        Assert.Equal("12 đường Lê Lợi, Phường Ba Đình, Thành phố Hà Nội, Việt Nam", geocoder.LastQuery);
    }

    [Theory]
    [InlineData("79", "00004")]
    [InlineData("01", "missing")]
    public async Task Create_rejects_a_cross_province_or_unknown_ward(
        string provinceCode,
        string communeCode)
    {
        await using var context = await CreateContextAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto(
                "Nhà trọ lỗi",
                "12 đường Lê Lợi",
                provinceCode,
                communeCode),
            ownerId: 10,
            isAdmin: true));

        Assert.Empty(context.Properties);
    }

    [Theory]
    [InlineData("12 đường Lê Lợi, Phường Ba Đình, Thành phố Hà Nội")]
    [InlineData("Phường Ba Đình, 12 đường Lê Lợi")]
    public async Task Create_rejects_administrative_names_repeated_in_the_street_field(
        string addressText)
    {
        await using var context = await CreateContextAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto(
                "Nhà trọ lỗi",
                addressText,
                "01",
                "00004"),
            ownerId: 10,
            isAdmin: true));

        Assert.Empty(context.Properties);
    }

    [Fact]
    public async Task Metadata_only_update_preserves_a_legacy_address_and_its_coordinates()
    {
        await using var context = await CreateContextAsync();
        context.Properties.Add(new Property
        {
            PropertyId = 21,
            OwnerId = 10,
            PropertyName = "Khu trọ cũ",
            AddressText = "12 Lê Lợi, Phường cũ, Quận cũ, Thành phố cũ",
            Ward = "Phường cũ",
            District = "Quận cũ",
            City = "Thành phố cũ",
            Latitude = 0,
            Longitude = 0
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var updated = await service.UpdateAsync(
            21,
            new UpdatePropertyDto(
                "Tên mới",
                "12 Lê Lợi, Phường cũ, Quận cũ, Thành phố cũ",
                "Phường cũ",
                "Quận cũ",
                "Thành phố cũ",
                "Mô tả mới"),
            requestingUserId: 10,
            isAdmin: true);

        Assert.NotNull(updated);
        var saved = await context.Properties.SingleAsync(item => item.PropertyId == 21);
        Assert.Equal("12 Lê Lợi, Phường cũ, Quận cũ, Thành phố cũ", saved.AddressText);
        Assert.Equal("Phường cũ", saved.Ward);
        Assert.Equal("Quận cũ", saved.District);
        Assert.Equal("Thành phố cũ", saved.City);
        Assert.Equal(0, saved.Latitude);
        Assert.Equal(0, saved.Longitude);
    }

    [Fact]
    public async Task Metadata_only_update_preserves_an_empty_legacy_address()
    {
        await using var context = await CreateContextAsync();
        context.Properties.Add(new Property
        {
            PropertyId = 22,
            OwnerId = 10,
            PropertyName = "Khu trọ cũ",
            AddressText = string.Empty,
            Ward = string.Empty,
            District = "Quận cũ",
            City = string.Empty,
            Latitude = 0,
            Longitude = 0
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var updated = await service.UpdateAsync(
            22,
            new UpdatePropertyDto(
                "Tên mới",
                string.Empty,
                string.Empty,
                "Quận cũ",
                string.Empty,
                "Mô tả mới"),
            requestingUserId: 10,
            isAdmin: true);

        Assert.NotNull(updated);
        var saved = await context.Properties.SingleAsync(item => item.PropertyId == 22);
        Assert.Equal(string.Empty, saved.AddressText);
        Assert.Equal(string.Empty, saved.Ward);
        Assert.Equal("Quận cũ", saved.District);
        Assert.Equal(string.Empty, saved.City);
        Assert.Equal(0, saved.Latitude);
        Assert.Equal(0, saved.Longitude);
    }

    [Fact]
    public async Task Address_change_without_administrative_codes_is_rejected()
    {
        await using var context = await CreateContextAsync();
        context.Properties.Add(new Property
        {
            PropertyId = 21,
            OwnerId = 10,
            PropertyName = "Khu trọ cũ",
            AddressText = "12 Lê Lợi",
            Ward = "Phường Ba Đình",
            District = string.Empty,
            City = "Thành phố Hà Nội",
            Latitude = 21.032m,
            Longitude = 105.814m
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.UpdateAsync(
            21,
            new UpdatePropertyDto(
                "Khu trọ cũ",
                "13 Lê Lợi",
                "Phường Ba Đình",
                string.Empty,
                "Thành phố Hà Nội"),
            requestingUserId: 10,
            isAdmin: true));
    }

    [Fact]
    public async Task Create_rejects_a_candidate_that_does_not_match_the_selected_ward()
    {
        await using var context = await CreateContextAsync();
        var candidate = new AddressGeocodingCandidate(
            new AddressCoordinates(21.032m, 105.814m),
            "12 đường Lê Lợi, Phường Ba Đình, Thành phố Hà Nội, Việt Nam",
            "Nominatim",
            "house",
            "12",
            "đường Lê Lợi",
            "Phường khác",
            "Thành phố Hà Nội",
            "vn");
        var service = new PropertyService(
            new PropertyRepository(context),
            new AdministrativeDivisionCatalog(),
            new FixedGeocoder(candidate));

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto("Nhà trọ", "12 đường Lê Lợi", "01", "00004"),
            ownerId: 10,
            isAdmin: true));
        Assert.Empty(context.Properties);
    }

    private static PropertyService CreateService(AppDbContext context) => new(
        new PropertyRepository(context),
        new AdministrativeDivisionCatalog());

    private static async Task<AppDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"property-address-{Guid.NewGuid()}")
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

    private sealed class FixedGeocoder(AddressGeocodingCandidate candidate) : IPropertyGeocodingService
    {
        public string? LastQuery { get; private set; }

        public Task<IReadOnlyList<AddressGeocodingCandidate>> FindNominatimCandidatesAsync(
            PropertyGeocodingQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query.FullAddress;
            return Task.FromResult<IReadOnlyList<AddressGeocodingCandidate>>([candidate]);
        }

        public Task<IReadOnlyList<AddressGeocodingCandidate>> FindPhotonCandidatesAsync(
            PropertyGeocodingQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AddressGeocodingCandidate>>(
                Array.Empty<AddressGeocodingCandidate>());
        }
    }
}
