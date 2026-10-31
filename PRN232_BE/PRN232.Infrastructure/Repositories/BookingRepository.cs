using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
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

    public Task<BookingRoomReadModel?> GetRoomAsync(int roomId) =>
        _context.Rooms
            .AsNoTracking()
            .Where(room => room.RoomId == roomId)
            .Select(room => new BookingRoomReadModel(room.RoomId, room.RoomType!.Price))
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

    public Task<List<BookingReadModel>> GetAllAsync(int? customerId, int? ownerId, string? status) =>
        InScope(customerId, ownerId)
            .Where(booking => status == null || booking.Status == status)
            .OrderByDescending(booking => booking.BookingId)
            .Select(ToReadModel)
            .ToListAsync();

    public Task<BookingReadModel?> GetByIdAsync(int bookingId, int? customerId, int? ownerId) =>
        InScope(customerId, ownerId)
            .Where(booking => booking.BookingId == bookingId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync();

    public Task<bool> ExistsAsync(int bookingId) =>
        _context.Bookings.AnyAsync(booking => booking.BookingId == bookingId);

    public Task<BookingPaymentReadModel?> GetForPaymentAsync(int bookingId) =>
        _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.BookingId == bookingId)
            .Select(booking => new BookingPaymentReadModel(
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

    public async Task<bool> CancelAsync(int bookingId, string expectedStatus, Notifications? customerNotification)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (_context.Database.IsNpgsql())
        {
            // Tạo hợp đồng cũng khoá dòng booking này, nên huỷ và lập hợp đồng không thể chạy chen nhau.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Bookings\" WHERE \"BookingId\" = {bookingId} FOR UPDATE");
        }

        // Chỉ huỷ khi trạng thái vẫn đúng như lúc service kiểm tra: nếu khách vừa thanh toán xong
        // (Pending -> Deposited) thì câu UPDATE không khớp dòng nào và lần huỷ này bị từ chối.
        // Booking đã lên hợp đồng thì không huỷ được nữa.
        var updated = await _context.Bookings
            .Where(booking => booking.BookingId == bookingId && booking.Status == expectedStatus &&
                booking.Contract == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(booking => booking.Status, BookingStatuses.Cancelled));
        if (updated == 0)
        {
            return false;
        }

        if (customerNotification is not null)
        {
            await _context.Notifications.AddAsync(customerNotification);
            await _context.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return true;
    }

    // Lọc dùng chung cho GetById và GetAll; tham số nào null thì không lọc theo tiêu chí đó.
    private IQueryable<Bookings> InScope(int? customerId, int? ownerId) =>
        _context.Bookings
            .AsNoTracking()
            .Where(booking => customerId == null || booking.CustomerId == customerId)
            .Where(booking => ownerId == null || booking.Room!.Property!.OwnerId == ownerId);

    private static readonly Expression<Func<Bookings, BookingReadModel>> ToReadModel = booking => new BookingReadModel(
        booking.BookingId,
        booking.RoomId,
        booking.Room!.RoomCode,
        booking.Room.PropertyId,
        booking.Room.Property!.PropertyName,
        booking.CustomerId,
        booking.Customer!.FullName,
        booking.DepositAmount,
        booking.Status,
        booking.ExpiryDate,
        booking.Customer.PhoneNumber,
        booking.Contract != null ? (int?)booking.Contract.ContractId : null);
}
