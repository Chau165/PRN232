namespace PRN232.Application.DTOs;

public record PropertyDto(
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
    int RoomCount,
    string? PrimaryImageUrl = null,
    string? Description = null,
    string ProvinceCode = "",
    string WardCode = "");

// OwnerId KHÔNG nằm trong Create/Update DTO — lấy từ claim "sub" của JWT (user đang
// đăng nhập), không tin client tự khai báo OwnerId của người khác.
public record CreatePropertyDto(
    string PropertyName,
    string AddressText,
    string? ProvinceCode,
    string? WardCode,
    string? Description = null,
    decimal? Latitude = null,
    decimal? Longitude = null);

// Có thể bỏ tọa độ khi địa chỉ và mã hành chính không đổi để giữ ghim cũ.
// Chỉ đổi ghim: gửi đủ cặp tọa độ, có thể giữ nguyên mã hành chính bằng cách bỏ hai mã.
public record UpdatePropertyDto(
    string PropertyName,
    string AddressText,
    string? ProvinceCode,
    string? WardCode,
    string? Description = null,
    decimal? Latitude = null,
    decimal? Longitude = null);

public sealed record AddressCoordinates(decimal Latitude, decimal Longitude);

public sealed record PropertyImageInput(string FileName, string ContentType, byte[] Content);
