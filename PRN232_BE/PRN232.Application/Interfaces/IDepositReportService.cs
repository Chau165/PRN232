using PRN232.Application.DTOs;
namespace PRN232.Application.Interfaces;

public interface IDepositReportService
{
    Task<DepositReportDto> GetAsync(DateOnly? fromMonth, DateOnly? toMonth, int? propertyId, int userId, string role, CancellationToken cancellationToken);
}
public record DepositReportRow(int PropertyId, string PropertyName, DateTime? AtUtc, decimal Collected, decimal Refunded);
public interface IDepositReportRepository
{
    Task<IReadOnlyList<DepositReportRow>> ReadAsync(int? propertyId, int? managerId, DateTime fromUtc, DateTime untilUtc, CancellationToken cancellationToken);
}
