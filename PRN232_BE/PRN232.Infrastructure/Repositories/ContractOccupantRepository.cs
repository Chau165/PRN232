using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class ContractOccupantRepository(AppDbContext context) : IContractOccupantRepository
{
    // IdentityNumber được EF giải mã khi đọc; service tự che trước khi trả ra API.
    public async Task<List<ContractOccupantReadModel>> GetAsync(int contractId, CancellationToken cancellationToken)
    {
        var rows = await context.ContractOccupants
            .AsNoTracking()
            .Where(occupant => occupant.ContractId == contractId)
            .OrderBy(occupant => occupant.ContractOccupantId)
            .Select(occupant => new
            {
                occupant.ContractOccupantId,
                occupant.FullName,
                occupant.PhoneNumber,
                occupant.IdentityNumber,
                occupant.MoveInDate,
                occupant.MoveOutDate,
                occupant.Note,
                AddedByName = occupant.AddedBy!.FullName,
                occupant.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new ContractOccupantReadModel(row.ContractOccupantId, row.FullName, row.PhoneNumber,
            row.IdentityNumber, row.MoveInDate, row.MoveOutDate, row.Note, row.AddedByName, row.CreatedAtUtc)).ToList();
    }

    public Task<int> GetRoomMaxOccupantsAsync(int roomId, CancellationToken cancellationToken) =>
        context.Rooms.Where(room => room.RoomId == roomId).Select(room => room.MaxOccupants).FirstAsync(cancellationToken);

    public async Task<bool> AddAsync(ContractOccupants occupant, int maxCoOccupants, IReadOnlyList<Notifications> notifications,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (context.Database.IsNpgsql())
        {
            // Hai lần thêm người song song không cùng vượt sức chứa phòng.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {occupant.ContractId} FOR UPDATE", cancellationToken);
        }
        var active = await context.ContractOccupants
            .CountAsync(item => item.ContractId == occupant.ContractId && item.MoveOutDate == null, cancellationToken);
        if (active >= maxCoOccupants)
        {
            return false;
        }
        context.ContractOccupants.Add(occupant);
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordMoveOutAsync(int contractId, int occupantId, DateOnly moveOutDate,
        IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var updated = await context.ContractOccupants
            .Where(occupant => occupant.ContractOccupantId == occupantId && occupant.ContractId == contractId &&
                occupant.MoveOutDate == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(occupant => occupant.MoveOutDate, moveOutDate), cancellationToken);
        if (updated == 0)
        {
            return false;
        }
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
