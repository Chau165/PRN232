using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class MaintenanceRepository : IMaintenanceRepository
{
    private readonly AppDbContext _context;

    public MaintenanceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<MaintenanceRequestDto>> GetAllAsync(int? customerId, string? status, int? assignedTo) =>
        await _context.MaintenanceRequests
            .AsNoTracking()
            .Where(r => customerId == null || r.Contract!.CustomerId == customerId)
            .Where(r => status == null || r.Status == status)
            .Where(r => assignedTo == null || r.AssignedTo == assignedTo)
            .OrderBy(r => r.RequestId)
            .Select(r => new MaintenanceRequestDto(
                r.RequestId,
                r.Description,
                r.Status,
                r.ContractId,
                r.Contract!.CustomerId,
                r.Contract.Customer!.FullName,
                r.Contract.RoomId,
                r.Contract.Room!.RoomCode,
                r.EquipmentId,
                r.Equipment == null ? null : r.Equipment.Name,
                r.Equipment == null ? null : r.Equipment.Status,
                r.AssignedTo,
                r.AssignedToUser == null ? null : r.AssignedToUser.FullName))
            .ToListAsync();

    public async Task<MaintenanceRequestDto?> GetByIdAsync(int id) =>
        await _context.MaintenanceRequests
            .AsNoTracking()
            .Where(r => r.RequestId == id)
            .Select(r => new MaintenanceRequestDto(
                r.RequestId,
                r.Description,
                r.Status,
                r.ContractId,
                r.Contract!.CustomerId,
                r.Contract.Customer!.FullName,
                r.Contract.RoomId,
                r.Contract.Room!.RoomCode,
                r.EquipmentId,
                r.Equipment == null ? null : r.Equipment.Name,
                r.Equipment == null ? null : r.Equipment.Status,
                r.AssignedTo,
                r.AssignedToUser == null ? null : r.AssignedToUser.FullName))
            .FirstOrDefaultAsync();

    public async Task<MaintenanceRequestStateDto?> GetStateByIdAsync(int id) =>
        await _context.MaintenanceRequests
            .AsNoTracking()
            .Where(r => r.RequestId == id)
            .Select(r => new MaintenanceRequestStateDto(
                r.RequestId, r.ContractId, r.EquipmentId, r.AssignedTo, r.Status))
            .FirstOrDefaultAsync();

    public async Task<MaintenanceContractDto?> GetContractAsync(int contractId) =>
        await _context.Contracts
            .AsNoTracking()
            .Where(c => c.ContractId == contractId)
            .Select(c => new MaintenanceContractDto(
                c.ContractId, c.RoomId, c.CustomerId, c.StartDate, c.EndDate, c.Status))
            .FirstOrDefaultAsync();

    public Task<int?> GetEquipmentRoomIdAsync(int equipmentId) =>
        _context.Equipments
            .AsNoTracking()
            .Where(e => e.EquipmentId == equipmentId)
            .Select(e => (int?)e.RoomId)
            .FirstOrDefaultAsync();

    public Task<bool?> GetUserActiveStatusAsync(int userId) =>
        _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => (bool?)u.IsActive)
            .FirstOrDefaultAsync();

    public async Task<MaintenanceRequests> AddAsync(MaintenanceRequests request)
    {
        await _context.MaintenanceRequests.AddAsync(request);
        return request;
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    public async Task<bool> UpdateStatusAndAddLogAsync(
        int requestId,
        string expectedStatus,
        string status,
        int? assignedTo,
        int? changedBy,
        DateTime changedAt)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var updatedCount = await _context.MaintenanceRequests
            .Where(r => r.RequestId == requestId && r.Status == expectedStatus)
            .ExecuteUpdateAsync(update => update
                .SetProperty(r => r.Status, status)
                .SetProperty(r => r.AssignedTo, assignedTo));

        if (updatedCount == 0)
        {
            return false;
        }

        _context.MaintenanceLogs.Add(new MaintenanceLogs
        {
            RequestId = requestId,
            OldStatus = expectedStatus,
            NewStatus = status,
            ChangedBy = changedBy,
            ChangedAt = DateTime.SpecifyKind(changedAt, DateTimeKind.Utc)
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<List<MaintenanceLogDto>?> GetLogsAsync(int requestId)
    {
        if (!await _context.MaintenanceRequests.AnyAsync(r => r.RequestId == requestId))
        {
            return null;
        }

        return await _context.MaintenanceLogs
            .AsNoTracking()
            .Where(log => log.RequestId == requestId)
            .OrderBy(log => log.ChangedAt)
            .ThenBy(log => log.LogId)
            .Select(log => new MaintenanceLogDto(
                log.LogId,
                log.RequestId,
                log.OldStatus,
                log.NewStatus,
                log.ChangedBy,
                log.ChangedByUser == null ? null : log.ChangedByUser.FullName,
                log.ChangedAt))
            .ToListAsync();
    }
}
