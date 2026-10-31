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

    public Task<ViewingRoomReadModel?> GetRoomAsync(int roomId) =>
        _context.Rooms
            .AsNoTracking()
            .Where(room => room.RoomId == roomId)
            .Select(room => new ViewingRoomReadModel(room.RoomId, room.RoomCode, room.Property!.OwnerId))
            .FirstOrDefaultAsync();

    public Task<bool> IsRoomRentableAsync(int roomId, DateTimeOffset asOfUtc) =>
        RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc).AnyAsync(room => room.RoomId == roomId);

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

    public async Task AddAsync(ViewingSchedules viewing, Notifications ownerNotification)
    {
        await _context.ViewingSchedules.AddAsync(viewing);
        await _context.Notifications.AddAsync(ownerNotification);
        await _context.SaveChangesAsync();
    }

    public Task<List<ViewingScheduleReadModel>> GetAllAsync(int? customerId, int? ownerId, string? status) =>
        InScope(customerId, ownerId)
            .Where(viewing => status == null || viewing.Status == status)
            .OrderByDescending(viewing => viewing.ViewingId)
            .Select(ToReadModel)
            .ToListAsync();

    public Task<ViewingScheduleReadModel?> GetByIdAsync(int viewingId, int? customerId, int? ownerId) =>
        InScope(customerId, ownerId)
            .Where(viewing => viewing.ViewingId == viewingId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync();

    public Task<bool> ExistsAsync(int viewingId) =>
        _context.ViewingSchedules.AnyAsync(viewing => viewing.ViewingId == viewingId);

    public async Task<bool> DecideAsync(
        int viewingId, string status, DateTime? scheduledAtUtc, Notifications customerNotification)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Điều kiện Pending nằm trong câu UPDATE nên hai quản lý bấm cùng lúc thì chỉ một người xử lý được.
        var updated = await _context.ViewingSchedules
            .Where(viewing => viewing.ViewingId == viewingId && viewing.Status == ViewingStatuses.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(viewing => viewing.Status, status)
                .SetProperty(viewing => viewing.ScheduledAt, viewing => scheduledAtUtc ?? viewing.ScheduledAt));
        if (updated == 0)
        {
            return false;
        }

        await _context.Notifications.AddAsync(customerNotification);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    // Lọc dùng chung cho GetById và GetAll; tham số nào null thì không lọc theo tiêu chí đó.
    private IQueryable<ViewingSchedules> InScope(int? customerId, int? ownerId) =>
        _context.ViewingSchedules
            .AsNoTracking()
            .Where(viewing => customerId == null || viewing.CustomerId == customerId)
            .Where(viewing => ownerId == null || viewing.Room!.Property!.OwnerId == ownerId);

    private static readonly Expression<Func<ViewingSchedules, ViewingScheduleReadModel>> ToReadModel =
        viewing => new ViewingScheduleReadModel(
            viewing.ViewingId,
            viewing.RoomId,
            viewing.Room!.RoomCode,
            viewing.Room.PropertyId,
            viewing.Room.Property!.PropertyName,
            viewing.CustomerId,
            viewing.Customer!.FullName,
            viewing.Customer.PhoneNumber,
            viewing.ScheduledAt,
            viewing.Status);
}
