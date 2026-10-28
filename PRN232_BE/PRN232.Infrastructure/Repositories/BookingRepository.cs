using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
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
}
