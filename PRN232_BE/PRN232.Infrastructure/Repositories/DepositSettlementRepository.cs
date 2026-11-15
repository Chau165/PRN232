using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class DepositSettlementRepository(AppDbContext context) : IDepositSettlementRepository
{
    public Task<DepositSettlementReadModel?> GetAsync(int contractId, CancellationToken cancellationToken) =>
        context.DepositSettlements
            .AsNoTracking()
            .Where(settlement => settlement.ContractId == contractId)
            .Select(settlement => new DepositSettlementReadModel(settlement.ContractId, settlement.MoveOutDate,
                settlement.DepositAmount, settlement.DamageCharges, settlement.OutstandingDebt, settlement.RetainedForDebt,
                settlement.ForfeitedAmount, settlement.RefundAmount, settlement.AmountDue, settlement.EarlyWithoutNotice,
                settlement.NoticeDays, settlement.Note, settlement.SettledBy!.FullName, settlement.SettledAtUtc,
                settlement.RefundConfirmedAtUtc,
                settlement.RefundConfirmedBy != null ? settlement.RefundConfirmedBy.FullName : null,
                settlement.DueCollectedAtUtc,
                settlement.DueCollectedBy != null ? settlement.DueCollectedBy.FullName : null))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAsync(int contractId, CancellationToken cancellationToken) =>
        context.DepositSettlements.AnyAsync(settlement => settlement.ContractId == contractId, cancellationToken);

    public async Task<bool> AddAsync(DepositSettlements settlement, Notifications customerNotification,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (context.Database.IsNpgsql())
        {
            // Trả phòng cũng khóa dòng hợp đồng: quyết toán và trả phòng không chạy chen nhau.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {settlement.ContractId} FOR UPDATE", cancellationToken);
        }
        if (!await context.Contracts.AnyAsync(contract => contract.ContractId == settlement.ContractId &&
                contract.Status == ContractStatuses.Active, cancellationToken))
        {
            return false;
        }
        context.DepositSettlements.Add(settlement);
        context.Notifications.Add(customerNotification);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Một lần quyết toán khác vừa lưu trước (UX_DepositSettlements_ContractId).
            return false;
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task<bool> ConfirmRefundAsync(int contractId, int userId, DateTime atUtc, Notifications customerNotification,
        CancellationToken cancellationToken) =>
        ConfirmAsync(context.DepositSettlements.Where(settlement => settlement.ContractId == contractId &&
                settlement.RefundAmount > 0m && settlement.RefundConfirmedAtUtc == null),
            setters => setters.SetProperty(settlement => settlement.RefundConfirmedAtUtc, atUtc)
                .SetProperty(settlement => settlement.RefundConfirmedById, userId),
            customerNotification, cancellationToken);

    public Task<bool> ConfirmDueCollectedAsync(int contractId, int userId, DateTime atUtc, Notifications customerNotification,
        CancellationToken cancellationToken) =>
        ConfirmAsync(context.DepositSettlements.Where(settlement => settlement.ContractId == contractId &&
                settlement.AmountDue > 0m && settlement.DueCollectedAtUtc == null),
            setters => setters.SetProperty(settlement => settlement.DueCollectedAtUtc, atUtc)
                .SetProperty(settlement => settlement.DueCollectedById, userId),
            customerNotification, cancellationToken);

    // Điều kiện "còn khoản và chưa xác nhận" nằm trong câu UPDATE: hai lần bấm song song chỉ một lần khớp.
    private async Task<bool> ConfirmAsync(IQueryable<DepositSettlements> pending,
        System.Linq.Expressions.Expression<Func<Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<DepositSettlements>,
            Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<DepositSettlements>>> setters,
        Notifications customerNotification, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (await pending.ExecuteUpdateAsync(setters, cancellationToken) == 0)
        {
            return false;
        }
        context.Notifications.Add(customerNotification);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
