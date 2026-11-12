using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IRevenueService
{
    Task<RevenueReportDto> GetAsync(DateOnly? fromMonth, DateOnly? toMonth, int? propertyId,
        int userId, string role, CancellationToken cancellationToken);
}
