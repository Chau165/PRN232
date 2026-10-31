namespace PRN232.Application.ReadModels;

public sealed record PublicPropertyPageReadModel(
    IReadOnlyList<PublicPropertyCardReadModel> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record PublicPropertyAreaCardReadModel(
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

public sealed record PublicPropertyCardReadModel(
    int PropertyId,
    string PropertyName,
    string AddressText,
    string Ward,
    string District,
    string City,
    string? PrimaryImageUrl,
    IReadOnlyList<PublicAmenitySummaryReadModel> AmenitiesSummary,
    int AvailableRoomCount,
    decimal? StartingPrice);

public sealed record PublicAmenitySummaryReadModel(int AmenityId, string Name);

public sealed record PublicPropertyServiceRateReadModel(
    int ServiceId,
    string ServiceName,
    decimal Price,
    string UnitCode,
    string BillingBasis,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record PublicPropertyDetailReadModel(
    int PropertyId,
    string PropertyName,
    string AddressText,
    string Ward,
    string District,
    string City,
    string? PrimaryImageUrl,
    int AvailableRoomCount,
    decimal? StartingPrice,
    IReadOnlyList<PublicPropertyRoomAmenityReadModel> RoomAmenities,
    string? Description = null,
    IReadOnlyList<PublicAmenitySummaryReadModel>? CommonAmenities = null,
    IReadOnlyList<PublicPropertyServiceRateReadModel>? LivingCosts = null);

public sealed record PublicPropertyRoomAmenityReadModel(int AmenityId, string Name, int RentableRoomCount);

public sealed record PublicPropertyRoomCardReadModel(
    int RoomId,
    string RoomCode,
    int RoomTypeId,
    string RoomTypeName,
    decimal BaseRent,
    decimal AreaSqm,
    string? PrimaryImageUrl,
    IReadOnlyList<PublicAmenitySummaryReadModel> AmenitiesSummary);

public sealed record PublicPropertyRoomPageReadModel(
    IReadOnlyList<PublicPropertyRoomCardReadModel> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record PublicRoomDetailReadModel(
    int RoomId,
    string RoomCode,
    decimal AreaSqm,
    PublicRoomTypeReadModel RoomType,
    decimal BaseRent,
    PublicRoomPropertyReadModel Property,
    IReadOnlyList<string> Images,
    IReadOnlyList<PublicAmenitySummaryReadModel> Amenities,
    IReadOnlyList<PublicRoomServiceReadModel> Services,
    IReadOnlyList<PublicRoomEquipmentReadModel> Equipments);

public sealed record PublicRoomTypeReadModel(int RoomTypeId, string RoomTypeName);

public sealed record PublicRoomPropertyReadModel(
    int PropertyId,
    string PropertyName,
    string AddressText,
    string Ward,
    string District,
    string City);

public sealed record PublicRoomServiceReadModel(int ServiceId, string Name, decimal Price);

public sealed record PublicRoomEquipmentReadModel(int EquipmentId, string Name);
