using PRN232.Application.DTOs;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IDepositSettlementRepository
{
    Task<DepositSettlementReadModel?> GetAsync(int contractId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(int contractId, CancellationToken cancellationToken);

    // false nếu hợp đồng đã được quyết toán (chỉ mục duy nhất ContractId) hoặc không còn Active.
    Task<bool> AddAsync(DepositSettlements settlement, Notifications customerNotification, CancellationToken cancellationToken);
}

public interface IDepositSettlementService
{
    Task<DepositSettlementDto> GetAsync(int contractId, int userId, string role, CancellationToken cancellationToken);
    Task<DepositSettlementDto> SettleAsync(int contractId, SettleDepositDto dto, int userId, string role,
        CancellationToken cancellationToken);
}
