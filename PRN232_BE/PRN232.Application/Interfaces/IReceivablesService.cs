using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IReceivablesService
{
    Task<ReceivablesReportDto> GetAsync(int? propertyId, DateOnly? asOfDate, int userId, string role, CancellationToken cancellationToken);
}
