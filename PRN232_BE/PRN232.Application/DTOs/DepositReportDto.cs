namespace PRN232.Application.DTOs;

public record DepositTotalsDto(decimal CollectedAmount, int CollectionCount, decimal RefundedAmount, int RefundCount)
{
    public decimal NetCashFlow => CollectedAmount - RefundedAmount;
}
public record DepositMonthDto(DateOnly Month, DepositTotalsDto Totals);
public record DepositPropertyDto(int PropertyId, string PropertyName, DepositTotalsDto Totals, DepositTotalsDto UnknownDate);
public record DepositReportDto(DateOnly FromMonth, DateOnly ToMonth, int? PropertyId,
    DepositTotalsDto Totals, DepositTotalsDto UnknownDate, IReadOnlyList<DepositMonthDto> Monthly,
    IReadOnlyList<DepositPropertyDto> ByProperty);
