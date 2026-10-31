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

// ElectricStart/WaterStart chỉ dùng cho lần ghi đầu tiên của hợp đồng;
// các tháng sau server tự lấy chỉ số cuối kỳ của lần ghi gần nhất.
public record CreateUtilityReadingDto(
    int ContractId,
    DateOnly ReadingMonth,
    decimal ElectricEnd,
    decimal WaterEnd,
    decimal? ElectricStart,
    decimal? WaterStart);
