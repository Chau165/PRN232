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

    // Rebuild 3.6c: chỉ ghi khi còn khoản đó và chưa xác nhận (bấm hai lần thì lần sau trả false).
    Task<bool> ConfirmRefundAsync(int contractId, int userId, DateTime atUtc, Notifications customerNotification,
        CancellationToken cancellationToken);
    Task<bool> ConfirmDueCollectedAsync(int contractId, int userId, DateTime atUtc, Notifications customerNotification,
        CancellationToken cancellationToken);
}

public interface IDepositSettlementService
{
    Task<DepositSettlementDto> GetAsync(int contractId, int userId, string role, CancellationToken cancellationToken);
    Task<DepositSettlementDto> SettleAsync(int contractId, SettleDepositDto dto, int userId, string role,
        CancellationToken cancellationToken);
    Task<DepositSettlementDto> ConfirmRefundAsync(int contractId, int userId, string role, CancellationToken cancellationToken);
    Task<DepositSettlementDto> ConfirmDueCollectedAsync(int contractId, int userId, string role, CancellationToken cancellationToken);
}
