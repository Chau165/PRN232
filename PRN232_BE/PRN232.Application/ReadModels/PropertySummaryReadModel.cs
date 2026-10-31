namespace PRN232.Application.ReadModels;

public sealed record PropertySummaryReadModel(
    int PropertyId,
    int OwnerId,
    string OwnerName,
    string PropertyName,
    string AddressText,
    string Ward,
    string District,
    string City,
    decimal Latitude,
    decimal Longitude,
    int RoomCount);
