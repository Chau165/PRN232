namespace PRN232.Application.ReadModels;

public sealed record RentalReadModel(
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
    IReadOnlyList<RentalEquipmentReadModel> Equipments,
    IReadOnlyList<RentalAmenityReadModel> Amenities);

public sealed record RentalEquipmentReadModel(int EquipmentId, string Name, string Status);

public sealed record RentalAmenityReadModel(int AmenityId, string Name);
