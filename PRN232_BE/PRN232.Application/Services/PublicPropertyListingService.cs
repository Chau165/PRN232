using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Normalizers;

namespace PRN232.Application.Services;

public sealed class PublicPropertyListingService : IPublicPropertyListingService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 10;
    private const int MaximumPageSize = 50;
    private const int MaximumAmenityIds = 20;
    private static readonly TimeZoneInfo PropertyBusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private static readonly string[] PropertySorts = ["propertyid", "price_asc", "price_desc"];
    private static readonly string[] RoomSorts = ["roomid", "price_asc", "price_desc"];

    private readonly IPublicPropertyListingRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly bool _propertyDetailEnrichmentEnabled;

    public PublicPropertyListingService(
        IPublicPropertyListingRepository repository,
        TimeProvider timeProvider,
        bool propertyDetailEnrichmentEnabled = false)
    {
        _repository = repository;
        _timeProvider = timeProvider;
        _propertyDetailEnrichmentEnabled = propertyDetailEnrichmentEnabled;
    }

    public async Task<PublicPropertyPageDto> GetAllAsync(
        PublicPropertyQueryDto query,
        CancellationToken cancellationToken)
    {
        var criteria = NormalizeAndValidate(
            query,
            "propertyid",
            PropertySorts,
            "Sort must be propertyId, price_asc, or price_desc.");
        if (criteria.AmenityIds.Length > 0 &&
            !await _repository.AmenityIdsExistAsync(criteria.AmenityIds, cancellationToken))
        {
            throw new PublicPropertyQueryValidationException("amenityIds", "One or more amenity IDs do not exist.");
        }

        var asOfUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        return await _repository.GetPageAsync(criteria, asOfUtc, cancellationToken);
    }

    public Task<IReadOnlyList<PublicPropertyAreaCardDto>> GetByAreaAsync(
        PublicPropertyAreaQueryDto query,
        CancellationToken cancellationToken)
    {
        var criteria = new PublicPropertyAreaSearchCriteria(
            NormalizeAreaFilter(query.City, "city"),
            NormalizeAreaFilter(query.District, "district"),
            NormalizeAreaFilter(query.Ward, "ward"));
        if (criteria.City is null && criteria.District is null && criteria.Ward is null)
        {
            throw new PublicPropertyQueryValidationException(
                "district",
                "Provide at least one nonblank city, district, or ward.");
        }

        var asOfUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        return _repository.GetByAreaAsync(criteria, asOfUtc, cancellationToken);
    }

    public async Task<PublicPropertyDetailDto?> GetDetailAsync(
        int propertyId,
        CancellationToken cancellationToken)
    {
        var asOfUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        if (!_propertyDetailEnrichmentEnabled)
        {
            return await _repository.GetDetailAsync(propertyId, asOfUtc, cancellationToken);
        }

        var localNow = TimeZoneInfo.ConvertTime(asOfUtc, PropertyBusinessTimeZone);
        var currentMonthStart = new DateOnly(localNow.Year, localNow.Month, 1);
        return await _repository.GetEnrichedDetailAsync(
            propertyId,
            asOfUtc,
            currentMonthStart,
            cancellationToken);
    }

    public async Task<PublicPropertyRoomPageDto?> GetRoomsAsync(
        int propertyId,
        PublicPropertyRoomsQueryDto query,
        CancellationToken cancellationToken)
    {
        var criteria = NormalizeAndValidate(
            new PublicPropertyQueryDto
            {
                MinPrice = query.MinPrice,
                MaxPrice = query.MaxPrice,
                RoomTypeName = query.RoomTypeName,
                AmenityIds = query.AmenityIds,
                SortBy = query.SortBy,
                Page = query.Page,
                PageSize = query.PageSize
            },
            "roomid",
            RoomSorts,
            "Sort must be roomId, price_asc, or price_desc.");
        if (criteria.AmenityIds.Length > 0 &&
            !await _repository.AmenityIdsExistAsync(criteria.AmenityIds, cancellationToken))
        {
            throw new PublicPropertyQueryValidationException("amenityIds", "One or more amenity IDs do not exist.");
        }

        var asOfUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        return await _repository.GetRoomsAsync(propertyId, criteria, asOfUtc, cancellationToken);
    }

    public Task<PublicRoomDetailDto?> GetRoomDetailAsync(int roomId, CancellationToken cancellationToken)
    {
        var asOfUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        return _repository.GetRoomDetailAsync(roomId, asOfUtc, cancellationToken);
    }

    private static PublicPropertySearchCriteria NormalizeAndValidate(
        PublicPropertyQueryDto query,
        string defaultSort,
        IReadOnlyCollection<string> supportedSorts,
        string sortError)
    {
        var page = query.Page ?? DefaultPage;
        if (page < 1)
        {
            throw new PublicPropertyQueryValidationException("page", "Page must be at least 1.");
        }

        var pageSize = query.PageSize ?? DefaultPageSize;
        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new PublicPropertyQueryValidationException("pageSize", $"Page size must be between 1 and {MaximumPageSize}.");
        }

        var offset = (long)(page - 1) * pageSize;
        if (offset > int.MaxValue)
        {
            throw new PublicPropertyQueryValidationException("page", "Page is too large.");
        }

        if (query.MinPrice < 0)
        {
            throw new PublicPropertyQueryValidationException("minPrice", "Minimum price cannot be negative.");
        }
        if (query.MaxPrice < 0)
        {
            throw new PublicPropertyQueryValidationException("maxPrice", "Maximum price cannot be negative.");
        }
        if (query.MinPrice is not null && query.MaxPrice is not null && query.MinPrice > query.MaxPrice)
        {
            throw new PublicPropertyQueryValidationException("maxPrice", "Maximum price must be greater than or equal to minimum price.");
        }

        var submittedAmenityIds = query.AmenityIds ?? [];
        if (submittedAmenityIds.Length > MaximumAmenityIds)
        {
            throw new PublicPropertyQueryValidationException("amenityIds", $"At most {MaximumAmenityIds} amenity IDs may be supplied.");
        }
        if (submittedAmenityIds.Any(amenityId => amenityId <= 0))
        {
            throw new PublicPropertyQueryValidationException("amenityIds", "Amenity IDs must be positive integers.");
        }

        var amenityIds = submittedAmenityIds.Distinct().Order().ToArray();
        var sortBy = NormalizeText(query.SortBy)?.ToLowerInvariant() ?? defaultSort;
        if (!supportedSorts.Contains(sortBy, StringComparer.Ordinal))
        {
            throw new PublicPropertyQueryValidationException("sortBy", sortError);
        }

        return new PublicPropertySearchCriteria(
            NormalizeText(query.Keyword),
            NormalizeText(query.City),
            NormalizeText(query.District),
            NormalizeText(query.Ward),
            query.MinPrice,
            query.MaxPrice,
            NormalizeText(query.RoomTypeName),
            amenityIds,
            sortBy,
            page,
            pageSize,
            (int)offset);
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeAreaFilter(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Trim().Length > 120)
        {
            throw new PublicPropertyQueryValidationException(field, "Area name must not exceed 120 characters.");
        }

        var normalized = PublicAreaNameNormalizer.Normalize(value);
        if (normalized.Length == 0)
        {
            throw new PublicPropertyQueryValidationException(field, "Area name must contain a place name.");
        }

        return normalized;
    }
}

public sealed class PublicPropertyQueryValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
