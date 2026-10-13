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
    int RoomCount);

// OwnerId KHÔNG nằm trong Create/Update DTO — lấy từ claim "sub" của JWT (user đang
// đăng nhập), không tin client tự khai báo OwnerId của người khác.
public record CreatePropertyDto(
    string PropertyName,
    string AddressText,
    string Ward,
    string District,
    string City,
    decimal Latitude,
    decimal Longitude);

public record UpdatePropertyDto(
    string PropertyName,
    string AddressText,
    string Ward,
    string District,
    string City,
    decimal Latitude,
    decimal Longitude);
