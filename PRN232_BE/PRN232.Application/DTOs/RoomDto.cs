namespace PRN232.Application.DTOs;

public record RoomDto(
    int RoomId,
    int PropertyId,
    string PropertyName,
    int RoomTypeId,
    string RoomTypeName,
    decimal RoomTypePrice,
    string RoomCode,
    decimal AreaSqm,
    string Status,
    string? PrimaryImageUrl,
    int MaxOccupants = 1,
    int? Floor = null,
    string? Description = null);

// MaxOccupants null: tạo mới thì lấy mức tối đa theo diện tích; sửa thì giữ giá trị đang lưu
// (giảm xuống nếu diện tích mới không còn đủ). Floor/Description gửi null = không có giá trị.
public record CreateRoomDto(
    int PropertyId,
    int RoomTypeId,
    string RoomCode,
    decimal AreaSqm,
    int? MaxOccupants = null,
    int? Floor = null,
    string? Description = null);

public record UpdateRoomDto(
    int RoomTypeId,
    string RoomCode,
    decimal AreaSqm,
    int? MaxOccupants = null,
    int? Floor = null,
    string? Description = null);

public record RoomImageDto(int RoomImageId, int RoomId, string ImageUrl, bool IsPrimary);

public record AddRoomImageDto(string ImageUrl, bool IsPrimary);

public sealed record RoomImageInput(string FileName, string ContentType, byte[] Content);
