using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class ViewingScheduleRepository : IViewingScheduleRepository
{
    private readonly AppDbContext _context;

    public ViewingScheduleRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ViewingRoomReadModel?> GetRoomAsync(int roomId)
    {
        return _context.Rooms
            .AsNoTracking()
            .Where(room => room.RoomId == roomId)
            .Select(room => new ViewingRoomReadModel(
                room.RoomId,
                room.RoomCode,
                room.PropertyId,
                room.RoomTypeId,
                room.Property!.OwnerId))
            .FirstOrDefaultAsync();
    }

    public Task<ViewingPropertyRoomTypeReadModel?> GetActivePropertyRoomTypeAsync(int propertyId, int roomTypeId)
    {
        return _context.PropertyRoomTypes
            .AsNoTracking()
            .Where(item => item.PropertyId == propertyId && item.RoomTypeId == roomTypeId && item.IsActive)
            .Select(item => new ViewingPropertyRoomTypeReadModel(
                item.PropertyId,
                item.Property!.PropertyName,
                item.RoomTypeId,
                item.RoomType!.TypeName,
                item.MonthlyRent))
            .FirstOrDefaultAsync();
    }

    public Task<ViewingManagerReadModel?> GetManagerAsync(int userId)
    {
        return _context.Users
            .AsNoTracking()
            .Where(user => user.UserId == userId)
            .Select(user => new ViewingManagerReadModel(
                user.UserId,
                user.FullName,
                user.PhoneNumber,
                user.Role!.RoleName,
                user.IsActive))
            .FirstOrDefaultAsync();
    }

    public Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId)
    {
        return _context.PropertyManagerAssignments.AnyAsync(assignment =>
            assignment.PropertyId == propertyId &&
            assignment.ManagerId == managerId &&
            assignment.Manager!.IsActive &&
            assignment.Manager.Role!.RoleName == RoleNames.Manager);
    }

    public Task<bool> IsRoomRentableAsync(int roomId, DateTimeOffset asOfUtc)
    {
        return RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc).AnyAsync(room => room.RoomId == roomId);
    }

    // Yêu cầu còn "mở" khi đang chờ hoặc đã hẹn mà chưa tới giờ; lịch đã qua giờ thì cho đặt lại.
    public Task<bool> HasOpenRequestAsync(int customerId, int roomId, DateTimeOffset asOfUtc)
    {
        var instantUtc = asOfUtc.UtcDateTime;
        return _context.ViewingSchedules.AnyAsync(viewing =>
            viewing.CustomerId == customerId &&
            viewing.RoomId == roomId &&
            (viewing.Status == ViewingStatuses.Pending || viewing.Status == ViewingStatuses.Approved) &&
            (viewing.ScheduledAt == null || viewing.ScheduledAt > instantUtc));
    }

    public Task<bool> HasOpenRequestForRoomTypeAsync(
        int customerId,
        int propertyId,
        int roomTypeId,
        DateTimeOffset asOfUtc)
    {
        var instantUtc = asOfUtc.UtcDateTime;
        return _context.ViewingSchedules.AnyAsync(viewing =>
            viewing.CustomerId == customerId &&
            viewing.PropertyId == propertyId &&
            viewing.RoomTypeId == roomTypeId &&
            (viewing.Status == ViewingStatuses.Pending || viewing.Status == ViewingStatuses.Approved) &&
            (viewing.ScheduledAt == null || viewing.ScheduledAt > instantUtc));
    }

    public Task<List<ViewingAvailableRoomReadModel>> GetAvailableRoomsAsync(
        int propertyId,
        int roomTypeId,
        DateTimeOffset asOfUtc)
    {
        return RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc)
            .Where(room => room.PropertyId == propertyId && room.RoomTypeId == roomTypeId)
            .OrderBy(room => room.RoomCode)
            .Select(room => new ViewingAvailableRoomReadModel(
                room.RoomId,
                room.RoomCode,
                room.AreaSqm,
                room.Status))
            .ToListAsync();
    }

    public async Task<bool> SelectRoomAsync(
        int viewingId,
        int managerId,
        int roomId,
        DateTimeOffset asOfUtc,
        Notifications notification)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"ViewingSchedules\" WHERE \"ViewingId\" = {viewingId} FOR UPDATE");
        }

        var viewing = await _context.ViewingSchedules
            .Where(item => item.ViewingId == viewingId)
            .Select(item => new { item.PropertyId, item.RoomTypeId })
            .FirstOrDefaultAsync();
        if (viewing is null)
        {
            return false;
        }

        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT assignment.\"PropertyId\" FROM \"PropertyManagerAssignments\" assignment WHERE assignment.\"PropertyId\" = {viewing.PropertyId} AND assignment.\"ManagerId\" = {managerId} FOR SHARE");
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Rooms\" WHERE \"RoomId\" = {roomId} FOR UPDATE");
        }

        if (!await HasActiveManagerAssignmentAsync(viewing.PropertyId, managerId))
        {
            return false;
        }

        var roomIsRentable = await RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc)
            .AnyAsync(room => room.RoomId == roomId &&
                room.PropertyId == viewing.PropertyId &&
                room.RoomTypeId == viewing.RoomTypeId);
        if (!roomIsRentable)
        {
            return false;
        }

        var updated = await _context.ViewingSchedules
            .Where(item => item.ViewingId == viewingId &&
                item.Status == ViewingStatuses.Approved &&
                item.AssignedTo == managerId &&
                item.RoomId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RoomId, (int?)roomId));
        if (updated == 0)
        {
            return false;
        }

        await _context.Notifications.AddAsync(notification);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task AddAsync(ViewingSchedules viewing, Func<int, IReadOnlyCollection<Notifications>> notificationFactory)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.ViewingSchedules.AddAsync(viewing);
        await _context.SaveChangesAsync();
        await _context.Notifications.AddRangeAsync(notificationFactory(viewing.ViewingId));
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public Task<List<ViewingScheduleReadModel>> GetAllAsync(int? customerId, int? assignedTo, string? status)
    {
        return InScope(customerId, assignedTo)
            .Where(viewing => status == null || viewing.Status == status)
            .OrderByDescending(viewing => viewing.ViewingId)
            .Select(ToReadModel)
            .ToListAsync();
    }

    public Task<ViewingScheduleReadModel?> GetByIdAsync(int viewingId, int? customerId, int? assignedTo)
    {
        return InScope(customerId, assignedTo)
            .Where(viewing => viewing.ViewingId == viewingId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync();
    }

    public Task<bool> ExistsAsync(int viewingId)
    {
        return _context.ViewingSchedules.AnyAsync(viewing => viewing.ViewingId == viewingId);
    }

    public async Task<bool> DecideAsync(
        int viewingId,
        string status,
        int? assignedTo,
        string? managerNote,
        int respondedBy,
        DateTime respondedAtUtc,
        DateTime? scheduledAtUtc,
        IReadOnlyCollection<Notifications> notifications)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        if (status == ViewingStatuses.Approved && assignedTo.HasValue)
        {
            var propertyId = await _context.ViewingSchedules
                .Where(viewing => viewing.ViewingId == viewingId)
                .Select(viewing => (int?)viewing.PropertyId)
                .FirstOrDefaultAsync();
            if (!propertyId.HasValue)
            {
                return false;
            }

            if (_context.Database.IsNpgsql())
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT assignment.\"PropertyId\" FROM \"PropertyManagerAssignments\" assignment WHERE assignment.\"PropertyId\" = {propertyId.Value} AND assignment.\"ManagerId\" = {assignedTo.Value} FOR SHARE");
            }

            if (!await HasActiveManagerAssignmentAsync(propertyId.Value, assignedTo.Value))
            {
                return false;
            }
        }

        // Điều kiện Pending nằm trong câu UPDATE nên hai quản lý bấm cùng lúc thì chỉ một người xử lý được.
        var updated = await _context.ViewingSchedules
            .Where(viewing => viewing.ViewingId == viewingId && viewing.Status == ViewingStatuses.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(viewing => viewing.Status, status)
                .SetProperty(viewing => viewing.AssignedTo, assignedTo)
                .SetProperty(viewing => viewing.ManagerNote, managerNote)
                .SetProperty(viewing => viewing.RespondedBy, (int?)respondedBy)
                .SetProperty(viewing => viewing.RespondedAt, (DateTime?)respondedAtUtc)
                .SetProperty(viewing => viewing.ScheduledAt, viewing => scheduledAtUtc ?? viewing.ScheduledAt));
        if (updated == 0)
        {
            return false;
        }

        await _context.Notifications.AddRangeAsync(notifications);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    // Lọc dùng chung cho GetById và GetAll; Manager scope luôn là AssignedTo, không phải OwnerId.
    private IQueryable<ViewingSchedules> InScope(int? customerId, int? assignedTo)
    {
        return _context.ViewingSchedules
            .AsNoTracking()
            .Where(viewing => customerId == null || viewing.CustomerId == customerId)
            .Where(viewing => assignedTo == null ||
                (viewing.AssignedTo == assignedTo &&
                 viewing.Property!.ManagerAssignments.Any(assignment =>
                     assignment.ManagerId == assignedTo &&
                      assignment.Manager!.IsActive &&
                     assignment.Manager.Role!.RoleName == RoleNames.Manager)));
    }

    private static readonly Expression<Func<ViewingSchedules, ViewingScheduleReadModel>> ToReadModel =
        viewing => new ViewingScheduleReadModel(
            viewing.ViewingId,
            viewing.RoomId,
            viewing.Room == null ? null : viewing.Room.RoomCode,
            viewing.PropertyId,
            viewing.Property!.PropertyName,
            viewing.RoomTypeId,
            viewing.RoomType!.TypeName,
            viewing.Property.PropertyRoomTypes
                .Where(item => item.RoomTypeId == viewing.RoomTypeId)
                .Select(item => (decimal?)item.MonthlyRent)
                .FirstOrDefault(),
            viewing.CustomerId,
            viewing.Customer!.FullName,
            viewing.Customer.PhoneNumber,
            viewing.ScheduledAt,
            viewing.Status,
            viewing.AssignedTo,
            viewing.AssignedManager == null ? null : viewing.AssignedManager.FullName,
            viewing.AssignedManager == null ? null : viewing.AssignedManager.PhoneNumber,
            viewing.ManagerNote,
            viewing.RespondedBy,
            viewing.RespondedAt);
}
