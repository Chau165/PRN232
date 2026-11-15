using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class HandoverRepository(AppDbContext context) : IHandoverRepository
{
    public Task<HandoverContractReadModel?> GetContractAsync(int contractId, CancellationToken cancellationToken) =>
        context.Contracts
            .AsNoTracking()
            .Where(contract => contract.ContractId == contractId)
            .Select(contract => new HandoverContractReadModel(contract.ContractId, contract.RoomId,
                contract.Room!.PropertyId, contract.CustomerId, contract.Status, contract.StartDate))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId, CancellationToken cancellationToken) =>
        context.PropertyManagerAssignments.AnyAsync(assignment =>
            assignment.PropertyId == propertyId &&
            assignment.ManagerId == managerId &&
            assignment.Manager!.IsActive &&
            assignment.Manager.Role!.RoleName == RoleNames.Manager, cancellationToken);

    public Task<List<HandoverEquipmentReadModel>> GetRoomEquipmentsAsync(int roomId, CancellationToken cancellationToken) =>
        context.Equipments
            .AsNoTracking()
            .Where(equipment => equipment.RoomId == roomId)
            .OrderBy(equipment => equipment.EquipmentId)
            .Select(equipment => new HandoverEquipmentReadModel(equipment.EquipmentId, equipment.Name,
                equipment.Quantity, equipment.CompensationValue))
            .ToListAsync(cancellationToken);

    public async Task<List<HandoverRecordReadModel>> GetRecordsAsync(int contractId, CancellationToken cancellationToken)
    {
        var records = await context.HandoverRecords
            .AsNoTracking()
            .Where(record => record.ContractId == contractId)
            .OrderBy(record => record.HandoverRecordId)
            .Select(record => new
            {
                record.HandoverRecordId,
                record.Kind,
                record.HandoverDate,
                record.Note,
                RecordedByName = record.RecordedBy!.FullName,
                record.RecordedAtUtc,
                Items = record.Items
                    .OrderBy(item => item.HandoverItemId)
                    .Select(item => new HandoverItemReadModel(item.HandoverItemId, item.EquipmentId, item.EquipmentName,
                        item.Quantity, item.CompensationValue, item.Condition, item.ChargeAmount, item.Note))
                    .ToList()
            })
            .ToListAsync(cancellationToken);
        return records.Select(record => new HandoverRecordReadModel(record.HandoverRecordId, record.Kind,
            record.HandoverDate, record.Note, record.RecordedByName, record.RecordedAtUtc, record.Items)).ToList();
    }

    public async Task<bool> SaveAsync(int contractId, string kind, DateOnly handoverDate, string? note, int recordedById,
        DateTime recordedAtUtc, IReadOnlyList<HandoverItemWriteModel> items, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (context.Database.IsNpgsql())
        {
            // Hai lần lưu biên bản của cùng hợp đồng chạy lần lượt, không chen nhau.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {contractId} FOR UPDATE", cancellationToken);
        }
        if (kind == HandoverKinds.MoveIn &&
            await context.HandoverRecords.AnyAsync(record => record.ContractId == contractId &&
                record.Kind == HandoverKinds.MoveOut, cancellationToken))
        {
            return false;
        }

        var record = await context.HandoverRecords
            .Include(existing => existing.Items)
            .FirstOrDefaultAsync(existing => existing.ContractId == contractId && existing.Kind == kind, cancellationToken);
        if (record is null)
        {
            record = new HandoverRecords { ContractId = contractId, Kind = kind };
            context.HandoverRecords.Add(record);
        }
        else
        {
            context.HandoverItems.RemoveRange(record.Items);
            record.Items.Clear();
        }

        record.HandoverDate = handoverDate;
        record.Note = note;
        record.RecordedById = recordedById;
        record.RecordedAtUtc = recordedAtUtc;
        foreach (var item in items)
        {
            record.Items.Add(new HandoverItems
            {
                EquipmentId = item.EquipmentId,
                EquipmentName = item.EquipmentName,
                Quantity = item.Quantity,
                CompensationValue = item.CompensationValue,
                Condition = item.Condition,
                ChargeAmount = item.ChargeAmount,
                Note = item.Note
            });
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Một lần lưu khác vừa tạo biên bản cùng loại (chỉ mục duy nhất ContractId + Kind).
            return false;
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
