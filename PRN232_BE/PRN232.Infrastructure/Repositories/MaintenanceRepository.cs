using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
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

    public async Task<List<MaintenanceRequestDto>> GetAllAsync(int? customerId, int? assignedManagerId, string? status, int? assignedTo) =>
        await _context.MaintenanceRequests
            .AsNoTracking()
            .Where(r => customerId == null || r.Contract!.CustomerId == customerId)
            .Where(r => assignedManagerId == null || r.AssignedTo == assignedManagerId)
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

    public async Task<MaintenanceRequestDto?> GetByIdAsync(int id, int? customerId, int? assignedManagerId) =>
        await _context.MaintenanceRequests
            .AsNoTracking()
            .Where(r => r.RequestId == id)
            .Where(r => customerId == null || r.Contract!.CustomerId == customerId)
            .Where(r => assignedManagerId == null || r.AssignedTo == assignedManagerId)
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

    public async Task<MaintenanceRequestStateDto?> GetStateByIdAsync(int id, int? assignedManagerId) =>
        await _context.MaintenanceRequests
            .AsNoTracking()
            .Where(r => r.RequestId == id)
            .Where(r => assignedManagerId == null || r.AssignedTo == assignedManagerId)
            .Select(r => new MaintenanceRequestStateDto(
                r.RequestId, r.ContractId, r.EquipmentId, r.AssignedTo, r.Status))
            .FirstOrDefaultAsync();

    public Task<bool> RequestExistsAsync(int id) => _context.MaintenanceRequests.AnyAsync(r => r.RequestId == id);

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

    public Task<MaintenanceUserAssignmentDto?> GetUserAssignmentAsync(int userId) =>
        _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new MaintenanceUserAssignmentDto(u.IsActive, u.Role!.RoleName))
            .FirstOrDefaultAsync();

    public async Task<MaintenanceRequests> AddWithInitialLogAsync(MaintenanceRequests request, MaintenanceLogs log)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        request.MaintenanceLogs.Add(log);
        await _context.MaintenanceRequests.AddAsync(request);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return request;
    }

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

    public async Task<List<MaintenanceLogDto>?> GetLogsAsync(int requestId, int? customerId, int? assignedManagerId)
    {
        var request = _context.MaintenanceRequests
            .Where(r => r.RequestId == requestId)
            .Where(r => customerId == null || r.Contract!.CustomerId == customerId)
            .Where(r => assignedManagerId == null || r.AssignedTo == assignedManagerId);
        if (!await request.AnyAsync())
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

    public Task<MaintenanceReportDto?> GetReportAsync(int requestId) =>
        _context.MaintenanceReports
            .AsNoTracking()
            .Where(report => report.RequestId == requestId)
            .Select(report => new MaintenanceReportDto(
                report.ReportId,
                report.RequestId,
                report.Description,
                report.ReportedBy,
                report.Reporter!.FullName,
                report.CreatedAt,
                report.Images.OrderBy(image => image.ImageId)
                    .Select(image => new MaintenanceReportImageDto(image.ImageId, image.ImageUrl))
                    .ToList()))
            .FirstOrDefaultAsync();

    public Task<bool> ReportExistsAsync(int requestId) =>
        _context.MaintenanceReports.AnyAsync(report => report.RequestId == requestId);

    public async Task<bool> CompleteWithReportAsync(int requestId, MaintenanceReports report, int changedBy, DateTime changedAt)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (await _context.MaintenanceReports.AnyAsync(item => item.RequestId == requestId))
        {
            return false;
        }

        var updatedCount = await _context.MaintenanceRequests
            .Where(item => item.RequestId == requestId &&
                           item.Status == MaintenanceRequestStatuses.Approved && item.AssignedTo == changedBy)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, MaintenanceRequestStatuses.Completed));
        if (updatedCount == 0)
        {
            return false;
        }

        report.RequestId = requestId;
        _context.MaintenanceReports.Add(report);
        _context.MaintenanceLogs.Add(new MaintenanceLogs
        {
            RequestId = requestId,
            OldStatus = MaintenanceRequestStatuses.Approved,
            NewStatus = MaintenanceRequestStatuses.Completed,
            ChangedBy = changedBy,
            ChangedAt = DateTime.SpecifyKind(changedAt, DateTimeKind.Utc)
        });
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }
}
