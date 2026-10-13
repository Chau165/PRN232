namespace PRN232.Application.DTOs;

public record EquipmentDto(
    int EquipmentId,
    int RoomId,
    string Name,
    DateOnly InstallDate,
    DateOnly WarrantyExpiry,
    string Supplier,
    string Status);

public record CreateEquipmentDto(
    int RoomId,
    string Name,
    DateOnly InstallDate,
    DateOnly WarrantyExpiry,
    string Supplier,
    string Status);

public record UpdateEquipmentDto(
    string Name,
    DateOnly InstallDate,
    DateOnly WarrantyExpiry,
    string Supplier,
    string Status);
