namespace PRN232.Application.DTOs;

public sealed record RentalDto(
    int ContractId,
    string ContractStatus,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal MonthlyRent,
    decimal DepositAmount,
    int RoomId,
    string RoomCode,
    string RoomStatus,
    decimal AreaSqm,
    int RoomTypeId,
    string RoomTypeName,
    int PropertyId,
    string PropertyName,
    string PropertyAddress,
    string? PrimaryImageUrl,
    IReadOnlyList<string> Images,
    IReadOnlyList<RentalEquipmentDto> Equipments,
    IReadOnlyList<RentalAmenityDto> Amenities);

public sealed record RentalEquipmentDto(int EquipmentId, string Name, string Status);

public sealed record RentalAmenityDto(int AmenityId, string Name);
