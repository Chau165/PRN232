namespace PRN232.Application.DTOs;

public record UtilityReadingDto(
    int ReadingId,
    int ContractId,
    string RoomCode,
    string PropertyName,
    string CustomerName,
    DateOnly ReadingMonth,
    decimal ElectricStart,
    decimal ElectricEnd,
    decimal ElectricUsage,
    decimal WaterStart,
    decimal WaterEnd,
    decimal WaterUsage);

// Các trường đầu kỳ cũ chỉ để tương thích: server dùng baseline hoặc kỳ trước.
public record CreateUtilityReadingDto(
    int ContractId,
    DateOnly ReadingMonth,
    decimal ElectricEnd,
    decimal WaterEnd,
    decimal? ElectricStart = null,
    decimal? WaterStart = null);
