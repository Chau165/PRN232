namespace PRN232.Application.DTOs;

public record RevenueReportDto(DateOnly FromMonth, DateOnly ToMonth, int? PropertyId,
    decimal TotalCollected, int PaymentCount, decimal UnknownDateAmount, int UnknownDatePaymentCount,
    IReadOnlyList<RevenueMonthDto> Monthly, IReadOnlyList<RevenuePropertyDto> ByProperty,
    IReadOnlyList<RevenuePropertyMonthDto> MonthlyByProperty);
public record RevenueMonthDto(DateOnly Month, decimal CollectedAmount, int PaymentCount);
public record RevenuePropertyDto(int PropertyId, string PropertyName, decimal CollectedAmount,
    int PaymentCount, decimal UnknownDateAmount, int UnknownDatePaymentCount);
public record RevenuePropertyMonthDto(DateOnly Month, int PropertyId, string PropertyName,
    decimal CollectedAmount, int PaymentCount);
