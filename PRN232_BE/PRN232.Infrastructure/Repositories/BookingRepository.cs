using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<BookingRoomDto?> GetRoomAsync(int roomId) =>
        _context.Rooms
            .AsNoTracking()
            .Where(room => room.RoomId == roomId)
            .Select(room => new BookingRoomDto(room.RoomId, room.RoomType!.Price))
            .FirstOrDefaultAsync();

    public async Task<bool> AddIfRoomRentableAsync(Bookings booking, DateTimeOffset asOfUtc)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        if (_context.Database.IsNpgsql())
        {
            // Khoá dòng phòng tới khi commit: hai khách đặt cùng lúc sẽ xếp hàng, người sau
            // kiểm tra lại sẽ thấy booking của người trước nên không thể cùng giữ một phòng.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Rooms\" WHERE \"RoomId\" = {booking.RoomId} FOR UPDATE");
        }

        var isRentable = await RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc)
            .AnyAsync(room => room.RoomId == booking.RoomId);
        if (!isRentable)
        {
            return false;
        }

        await _context.Bookings.AddAsync(booking);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public Task<BookingDto?> GetByIdAsync(int bookingId, int? customerId, int? ownerId) =>
        _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.BookingId == bookingId)
            .Where(booking => customerId == null || booking.CustomerId == customerId)
            .Where(booking => ownerId == null || booking.Room!.Property!.OwnerId == ownerId)
            .Select(booking => new BookingDto(
                booking.BookingId,
                booking.RoomId,
                booking.Room!.RoomCode,
                booking.Room.PropertyId,
                booking.Room.Property!.PropertyName,
                booking.CustomerId,
                booking.Customer!.FullName,
                booking.DepositAmount,
                booking.Status,
                booking.ExpiryDate))
            .FirstOrDefaultAsync();

    public Task<bool> ExistsAsync(int bookingId) =>
        _context.Bookings.AnyAsync(booking => booking.BookingId == bookingId);

    public Task<BookingPaymentDto?> GetForPaymentAsync(int bookingId) =>
        _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.BookingId == bookingId)
            .Select(booking => new BookingPaymentDto(
                booking.BookingId,
                booking.CustomerId,
                booking.Room!.Property!.OwnerId,
                booking.Room.RoomCode,
                booking.DepositAmount,
                booking.Status,
                booking.ExpiryDate))
            .FirstOrDefaultAsync();

    public async Task<bool> MarkDepositedAsync(
        int bookingId, DateTimeOffset asOfUtc, DateTime holdUntilUtc, Payments payment, Notifications notification)
    {
        var instantUtc = asOfUtc.UtcDateTime;
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Điều kiện Pending + còn hạn nằm ngay trong câu UPDATE: hai lần thanh toán chạy song song thì
        // lần sau không còn khớp dòng nào (0 dòng) nên một booking không thể được ghi nhận cọc hai lần.
        var updated = await _context.Bookings
            .Where(booking => booking.BookingId == bookingId &&
                booking.Status == BookingStatuses.Pending &&
                booking.ExpiryDate > instantUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(booking => booking.Status, BookingStatuses.Deposited)
                .SetProperty(booking => booking.ExpiryDate, holdUntilUtc));
        if (updated == 0)
        {
            return false;
        }

        await _context.Payments.AddAsync(payment);
        await _context.Notifications.AddAsync(notification);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }
}
