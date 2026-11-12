namespace PRN232.Application.ReadModels;

public sealed record EquipmentReadModel(
    int EquipmentId,
    int RoomId,
    string RoomCode,
    int PropertyId,
    string PropertyName,
    string Name,
    DateOnly InstallDate,
    DateOnly WarrantyExpiry,
    string Supplier,
    string Status);
