using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class ContractTransferRepository(AppDbContext context) : IContractTransferRepository
{
    public Task<TransferUserReadModel?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.ToLower();
        return context.Users.AsNoTracking()
            .Where(user => user.Email.ToLower() == normalized)
            .Select(user => new TransferUserReadModel(user.UserId, user.FullName, user.Email, user.Role!.RoleName, user.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<string?> GetActiveOccupantNameAsync(int contractId, int occupantId, CancellationToken cancellationToken) =>
        context.ContractOccupants.AsNoTracking()
            .Where(occupant => occupant.ContractOccupantId == occupantId && occupant.ContractId == contractId &&
                occupant.MoveOutDate == null)
            .Select(occupant => occupant.FullName)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ContractTransferReadModel?> GetAsync(int transferId, CancellationToken cancellationToken) =>
        Project(context.ContractTransfers.AsNoTracking()
                .Where(transfer => transfer.ContractTransferId == transferId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<ContractTransferReadModel>> ListAsync(int? customerId, int? managerId, CancellationToken cancellationToken) =>
        Project(context.ContractTransfers.AsNoTracking()
                .Where(transfer => customerId == null ||
                    transfer.FromContract!.CustomerId == customerId || transfer.NewHolderId == customerId)
                .Where(transfer => managerId == null || transfer.FromContract!.Room!.Property!.ManagerAssignments.Any(assignment =>
                    assignment.ManagerId == managerId && assignment.Manager!.IsActive &&
                    assignment.Manager.Role!.RoleName == RoleNames.Manager))
                .OrderByDescending(transfer => transfer.ContractTransferId))
            .ToListAsync(cancellationToken);

    public async Task<bool> AddAsync(ContractTransfers transfer, IReadOnlyList<Notifications> notifications,
        CancellationToken cancellationToken)
    {
        context.ContractTransfers.Add(transfer);
        context.Notifications.AddRange(notifications);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // UX_ContractTransfers_PendingPerContract: hợp đồng đã có một đề nghị đang chờ.
            return false;
        }
    }

    public async Task<bool> ConfirmAsync(int transferId, bool asOldHolder, DateTime atUtc, IReadOnlyList<Notifications> notifications,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var pending = context.ContractTransfers.Where(transfer =>
            transfer.ContractTransferId == transferId && transfer.Status == ContractTransferStatuses.Pending);
        var updated = asOldHolder
            ? await pending.Where(transfer => transfer.OldHolderConfirmedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(transfer => transfer.OldHolderConfirmedAtUtc, atUtc), cancellationToken)
            : await pending.Where(transfer => transfer.NewHolderConfirmedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(transfer => transfer.NewHolderConfirmedAtUtc, atUtc), cancellationToken);
        return await FinishAsync(transaction, updated, notifications, cancellationToken);
    }

    public async Task<bool> CancelAsync(int transferId, int userId, DateTime atUtc, string? reason,
        IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var updated = await context.ContractTransfers
            .Where(transfer => transfer.ContractTransferId == transferId && transfer.Status == ContractTransferStatuses.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(transfer => transfer.Status, ContractTransferStatuses.Cancelled)
                .SetProperty(transfer => transfer.CancelledAtUtc, atUtc)
                .SetProperty(transfer => transfer.CancelledById, userId)
                .SetProperty(transfer => transfer.CancelReason, reason), cancellationToken);
        return await FinishAsync(transaction, updated, notifications, cancellationToken);
    }

    public async Task<ContractTransferFactsReadModel> GetFactsAsync(int fromContractId, int? becomingHolderOccupantId,
        DateOnly effectiveDate, CancellationToken cancellationToken)
    {
        var contract = await context.Contracts.AsNoTracking()
            .Where(item => item.ContractId == fromContractId)
            .Select(item => new
            {
                item.InitialElectricReading,
                item.InitialWaterReading,
                item.MeterHandoverDate,
                item.Room!.MaxOccupants
            })
            .SingleAsync(cancellationToken);
        var readings = context.UtilityReadings.AsNoTracking().Where(reading => reading.ContractId == fromContractId);
        var latest = await readings.OrderByDescending(reading => reading.ReadingMonth)
            .Select(reading => new { reading.ReadingMonth, reading.ElectricEnd, reading.WaterEnd })
            .FirstOrDefaultAsync(cancellationToken);
        var invoices = context.Invoices.AsNoTracking().Where(invoice => invoice.ContractId == fromContractId);
        var moveOut = await context.HandoverRecords.AsNoTracking()
            .Where(record => record.ContractId == fromContractId && record.Kind == HandoverKinds.MoveOut)
            .Select(record => new
            {
                Damaged = record.Items.Any(item =>
                    item.Condition == EquipmentConditions.Damaged || item.Condition == EquipmentConditions.Missing)
            })
            .FirstOrDefaultAsync(cancellationToken);
        var activeOccupants = context.ContractOccupants.AsNoTracking()
            .Where(occupant => occupant.ContractId == fromContractId && occupant.MoveOutDate == null);

        return new ContractTransferFactsReadModel(
            latest?.ReadingMonth,
            latest?.ElectricEnd,
            latest?.WaterEnd,
            contract.InitialElectricReading.HasValue && contract.InitialWaterReading.HasValue && contract.MeterHandoverDate.HasValue,
            contract.InitialElectricReading,
            contract.InitialWaterReading,
            await readings.AnyAsync(reading => reading.ReadingMonth >= effectiveDate, cancellationToken),
            await invoices.AnyAsync(invoice => invoice.InvoiceMonth >= effectiveDate, cancellationToken),
            await invoices.AnyAsync(invoice => invoice.InvoiceMonth == effectiveDate.AddMonths(-1), cancellationToken),
            moveOut is not null,
            moveOut?.Damaged == true,
            await context.DepositSettlements.AnyAsync(settlement => settlement.ContractId == fromContractId, cancellationToken),
            await activeOccupants.CountAsync(cancellationToken),
            becomingHolderOccupantId is { } occupantId &&
                await activeOccupants.AnyAsync(occupant => occupant.ContractOccupantId == occupantId, cancellationToken),
            contract.MaxOccupants);
    }

    public async Task<int?> CompleteAsync(ContractTransferCompletion completion, IReadOnlyList<Notifications> notifications,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var transfer = await context.ContractTransfers
            .SingleOrDefaultAsync(item => item.ContractTransferId == completion.ContractTransferId, cancellationToken);
        if (transfer is null)
        {
            return null;
        }
        if (context.Database.IsNpgsql())
        {
            // Cùng thứ tự khóa với trả phòng: hợp đồng rồi phòng. Hai lần hoàn tất song song chỉ một lần chạy được.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {transfer.FromContractId} FOR UPDATE", cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Rooms\" WHERE \"RoomId\" = (SELECT \"RoomId\" FROM \"Contracts\" WHERE \"ContractId\" = {transfer.FromContractId}) FOR UPDATE",
                cancellationToken);
            await context.Entry(transfer).ReloadAsync(cancellationToken);
        }
        var old = await context.Contracts.SingleAsync(item => item.ContractId == transfer.FromContractId, cancellationToken);
        if (transfer.Status != ContractTransferStatuses.Pending || transfer.OldHolderConfirmedAtUtc is null ||
            transfer.NewHolderConfirmedAtUtc is null || old.Status != ContractStatuses.Active ||
            await context.Contracts.AnyAsync(item => item.TransferredFromContractId == old.ContractId, cancellationToken))
        {
            return null;
        }

        var effective = transfer.EffectiveDate;
        var lastDay = effective.AddDays(-1);
        var created = new Contracts
        {
            RoomId = old.RoomId,
            CustomerId = transfer.NewHolderId,
            ProcessedByStaffId = completion.ManagerId,
            StartDate = effective,
            EndDate = old.EndDate,
            MonthlyRent = old.MonthlyRent,
            DepositAmount = completion.DepositCarriedOver,
            Status = ContractStatuses.Active,
            InitialElectricReading = completion.InitialElectricReading,
            InitialWaterReading = completion.InitialWaterReading,
            MeterHandoverDate = completion.InitialElectricReading.HasValue ? effective : null,
            TransferredFromContractId = old.ContractId
        };
        context.Contracts.Add(created);

        // Hợp đồng cũ kết thúc vào ngày trước ngày hiệu lực. Phòng vẫn Occupied vì hợp đồng mới tiếp nối ngay.
        old.Status = ContractStatuses.Terminated;
        if (old.EndDate > lastDay)
        {
            old.EndDate = lastDay;
        }

        // Người ở cùng: đóng ở hợp đồng cũ; ai không phải người nhận thì chuyển sang hợp đồng mới.
        var occupants = await context.ContractOccupants
            .Where(occupant => occupant.ContractId == old.ContractId && occupant.MoveOutDate == null)
            .ToListAsync(cancellationToken);
        foreach (var occupant in occupants)
        {
            occupant.MoveOutDate = occupant.MoveInDate > lastDay ? occupant.MoveInDate : lastDay;
            if (occupant.ContractOccupantId == transfer.BecomingHolderOccupantId)
            {
                continue;
            }
            context.ContractOccupants.Add(new ContractOccupants
            {
                Contract = created,
                FullName = occupant.FullName,
                PhoneNumber = occupant.PhoneNumber,
                IdentityNumber = occupant.IdentityNumber,
                MoveInDate = effective,
                Note = $"Chuyển từ hợp đồng #{old.ContractId}",
                AddedById = completion.ManagerId,
                CreatedAtUtc = completion.CompletedAtUtc
            });
        }

        // Biên bản kiểm tra thiết bị lúc chuyển trở thành biên bản nhận phòng của hợp đồng mới.
        var inspection = await context.HandoverRecords.Include(record => record.Items)
            .SingleAsync(record => record.ContractId == old.ContractId && record.Kind == HandoverKinds.MoveOut, cancellationToken);
        context.HandoverRecords.Add(new HandoverRecords
        {
            Contract = created,
            Kind = HandoverKinds.MoveIn,
            HandoverDate = effective,
            Note = $"Chuyển từ hợp đồng #{old.ContractId}",
            RecordedById = completion.ManagerId,
            RecordedAtUtc = completion.CompletedAtUtc,
            Items = inspection.Items.Select(item => new HandoverItems
            {
                EquipmentId = item.EquipmentId,
                EquipmentName = item.EquipmentName,
                Quantity = item.Quantity,
                CompensationValue = item.CompensationValue,
                Condition = item.Condition,
                ChargeAmount = 0m,
                Note = item.Note
            }).ToList()
        });

        transfer.Status = ContractTransferStatuses.Completed;
        transfer.CompletedAtUtc = completion.CompletedAtUtc;
        transfer.CompletedById = completion.ManagerId;
        transfer.NewContract = created;
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created.ContractId;
    }

    private async Task<bool> FinishAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, int updated,
        IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken)
    {
        if (updated == 0)
        {
            return false;
        }
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // Tên người ở cùng (sẽ đứng tên) lấy bằng truy vấn con; họ tên không mã hóa nên đọc thẳng được.
    private IQueryable<ContractTransferReadModel> Project(IQueryable<ContractTransfers> query) =>
        query.Select(transfer => new ContractTransferReadModel(
            transfer.ContractTransferId,
            transfer.Status,
            transfer.FromContractId,
            transfer.FromContract!.Room!.PropertyId,
            transfer.FromContract.Room.RoomCode,
            transfer.FromContract.Room.Property!.PropertyName,
            transfer.FromContract.CustomerId,
            transfer.FromContract.Customer!.FullName,
            transfer.NewHolderId,
            transfer.NewHolder!.FullName,
            transfer.NewHolder.Email,
            transfer.BecomingHolderOccupantId,
            context.ContractOccupants
                .Where(occupant => occupant.ContractOccupantId == transfer.BecomingHolderOccupantId)
                .Select(occupant => occupant.FullName)
                .FirstOrDefault(),
            transfer.EffectiveDate,
            transfer.RequestedBy!.FullName,
            transfer.CreatedAtUtc,
            transfer.OldHolderConfirmedAtUtc,
            transfer.NewHolderConfirmedAtUtc,
            transfer.CompletedAtUtc,
            transfer.NewContractId,
            transfer.CancelledAtUtc,
            transfer.CancelReason));
}
