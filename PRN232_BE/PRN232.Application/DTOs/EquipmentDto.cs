namespace PRN232.Application.DTOs;

public record EquipmentDto(
    int EquipmentId,
    int RoomId,
    string Name,
    DateOnly InstallDate,
    DateOnly WarrantyExpiry,
    string Supplier,
    string Status,
    string? RoomCode = null,
    int? PropertyId = null,
    string? PropertyName = null,
    int Quantity = 1,
    decimal CompensationValue = 0m);

// Quantity/CompensationValue null: tạo mới thì 1 cái và chưa định giá (0đ); sửa thì giữ giá trị
// đang lưu. Nhờ vậy FE và mobile cũ chưa có hai ô này vẫn tạo/sửa thiết bị được.
public record CreateEquipmentDto(
    int RoomId,
    string Name,
    DateOnly InstallDate,
    DateOnly WarrantyExpiry,
    string Supplier,
    string Status,
    int? Quantity = null,
    decimal? CompensationValue = null);

public record UpdateEquipmentDto(
    string Name,
    DateOnly InstallDate,
    DateOnly WarrantyExpiry,
    string Supplier,
    string Status,
    int? Quantity = null,
    decimal? CompensationValue = null);
