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
    decimal WaterUsage,
    IReadOnlyList<string>? Warnings = null);

// Các trường đầu kỳ cũ chỉ để tương thích: server dùng baseline hoặc kỳ trước.
public record CreateUtilityReadingDto(
    int ContractId,
    DateOnly ReadingMonth,
    decimal ElectricEnd,
    decimal WaterEnd,
    decimal? ElectricStart = null,
    decimal? WaterStart = null);

// Rebuild 4.4: kết quả xem trước khi lưu chỉ số (không ghi gì vào DB). Warnings rỗng = bình thường.
public record UtilityReadingPreviewDto(
    int ContractId,
    DateOnly ReadingMonth,
    decimal ElectricStart,
    decimal ElectricEnd,
    decimal ElectricUsage,
    decimal WaterStart,
    decimal WaterEnd,
    decimal WaterUsage,
    decimal? AverageElectricUsage,
    decimal? AverageWaterUsage,
    int MonthsAveraged,
    IReadOnlyList<string> Warnings);
