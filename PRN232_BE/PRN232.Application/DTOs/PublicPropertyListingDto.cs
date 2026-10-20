namespace PRN232.Application.DTOs;

public sealed class PublicPropertyQueryDto
{
    public string? Keyword { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? RoomTypeName { get; set; }
    public int[]? AmenityIds { get; set; }
    public string? SortBy { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public sealed class PublicPropertyAreaQueryDto
{
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
}

public sealed record PublicPropertySearchCriteria(
    string? Keyword,
    string? City,
    string? District,
    string? Ward,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? RoomTypeName,
    int[] AmenityIds,
    string SortBy,
    int Page,
    int PageSize,
    int Offset);

public sealed record PublicPropertyPageDto(
    IReadOnlyList<PublicPropertyCardDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record PublicPropertyAreaSearchCriteria(
    string? City,
    string? District,
    string? Ward);

public sealed record PublicPropertyAreaCardDto(
    int PropertyId,
    string PropertyName,
    string AddressText,
    string Ward,
    string District,
    string City,
    decimal Latitude,
    decimal Longitude,
    string? PrimaryImageUrl,
    int AvailableRoomCount,
    decimal? StartingPrice);

public sealed record PublicPropertyCardDto(
    int PropertyId,
    string PropertyName,
    string AddressText,
    string Ward,
    string District,
    string City,
    string? PrimaryImageUrl,
    IReadOnlyList<PublicAmenitySummaryDto> AmenitiesSummary,
    int AvailableRoomCount,
    decimal? StartingPrice);

public sealed record PublicAmenitySummaryDto(int AmenityId, string Name);

public sealed record PublicPropertyServiceRateDto(
    int ServiceId,
    string ServiceName,
    decimal Price,
    string UnitCode,
    string BillingBasis,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record PublicPropertyDetailDto(
    int PropertyId,
    string PropertyName,
    string AddressText,
    string Ward,
    string District,
    string City,
    string? PrimaryImageUrl,
    int AvailableRoomCount,
    decimal? StartingPrice,
    IReadOnlyList<PublicPropertyRoomAmenityDto> RoomAmenities)
{
    public string? Description { get; init; }

    public IReadOnlyList<PublicAmenitySummaryDto> CommonAmenities { get; init; } =
        Array.Empty<PublicAmenitySummaryDto>();

    public IReadOnlyList<PublicPropertyServiceRateDto> LivingCosts { get; init; } =
        Array.Empty<PublicPropertyServiceRateDto>();
}

public sealed record PublicPropertyRoomAmenityDto(
    int AmenityId,
    string Name,
    int RentableRoomCount);

public sealed class PublicPropertyRoomsQueryDto
{
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? RoomTypeName { get; set; }
    public int[]? AmenityIds { get; set; }
    public string? SortBy { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public sealed record PublicPropertyRoomCardDto(
    int RoomId,
    string RoomCode,
    int RoomTypeId,
    string RoomTypeName,
    decimal BaseRent,
    decimal AreaSqm,
    string? PrimaryImageUrl,
    IReadOnlyList<PublicAmenitySummaryDto> AmenitiesSummary);

public sealed record PublicPropertyRoomPageDto(
    IReadOnlyList<PublicPropertyRoomCardDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record PublicRoomDetailDto(
    int RoomId,
    string RoomCode,
    decimal AreaSqm,
    PublicRoomTypeDto RoomType,
    decimal BaseRent,
    PublicRoomPropertyDto Property,
    IReadOnlyList<string> Images,
    IReadOnlyList<PublicAmenitySummaryDto> Amenities,
    IReadOnlyList<PublicRoomServiceDto> Services,
    IReadOnlyList<PublicRoomEquipmentDto> Equipments);

public sealed record PublicRoomTypeDto(int RoomTypeId, string RoomTypeName);

public sealed record PublicRoomPropertyDto(
    int PropertyId,
    string PropertyName,
    string AddressText,
    string Ward,
    string District,
    string City);

public sealed record PublicRoomServiceDto(int ServiceId, string Name, decimal Price);

public sealed record PublicRoomEquipmentDto(int EquipmentId, string Name);
