using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
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

    public Task<BookingRoomReadModel?> GetRoomAsync(int roomId)
    {
        return _context.Rooms
            .AsNoTracking()
            .Where(room => room.RoomId == roomId)
            .Select(room => new BookingRoomReadModel(
                room.RoomId,
                room.RoomCode,
                room.Property!.OwnerId,
                room.PropertyRoomType!.MonthlyRent))
            .FirstOrDefaultAsync();
    }

    public Task<ViewingBookingReadModel?> GetViewingForBookingAsync(int viewingId, int customerId)
    {
        return _context.ViewingSchedules
            .AsNoTracking()
            .Where(viewing => viewing.ViewingId == viewingId && viewing.CustomerId == customerId)
            .Select(viewing => new ViewingBookingReadModel(
                viewing.ViewingId,
                viewing.RoomId,
                viewing.CustomerId,
                viewing.PropertyId,
                viewing.RoomTypeId,
                viewing.Status))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> AddIfRoomRentableAsync(
        Bookings booking, Func<int, Notifications> notificationFactory, DateTimeOffset asOfUtc)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        if (_context.Database.IsNpgsql())
        {
            // Keep the same lock order as Manager room selection: ViewingSchedule, then Room.
            if (booking.ViewingId.HasValue)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"ViewingSchedules\" WHERE \"ViewingId\" = {booking.ViewingId.Value} FOR UPDATE");
            }
            // Locking Room serializes direct bookings and bookings originating from separate viewings.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Rooms\" WHERE \"RoomId\" = {booking.RoomId} FOR UPDATE");
        }

        if (booking.ViewingId.HasValue)
        {
            var viewingIsValid = await _context.ViewingSchedules.AnyAsync(viewing =>
                viewing.ViewingId == booking.ViewingId.Value &&
                viewing.CustomerId == booking.CustomerId &&
                viewing.RoomId == booking.RoomId &&
                viewing.Status == ViewingStatuses.Approved);
            var alreadyBookedFromViewing = await _context.Bookings.AnyAsync(item => item.ViewingId == booking.ViewingId);
            if (!viewingIsValid || alreadyBookedFromViewing)
            {
                return false;
            }
        }

        var isRentable = await RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc)
            .AnyAsync(room => room.RoomId == booking.RoomId);
        if (!isRentable) return false;

        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"PropertyRoomTypes\" WHERE \"PropertyId\" = (SELECT \"PropertyId\" FROM \"Rooms\" WHERE \"RoomId\" = {booking.RoomId}) AND \"RoomTypeId\" = (SELECT \"RoomTypeId\" FROM \"Rooms\" WHERE \"RoomId\" = {booking.RoomId}) AND \"IsActive\" = TRUE FOR SHARE");
        }

        var agreedMonthlyRent = await _context.Rooms
            .Where(room => room.RoomId == booking.RoomId &&
                room.PropertyRoomType != null && room.PropertyRoomType.IsActive)
            .Select(room => (decimal?)room.PropertyRoomType!.MonthlyRent)
            .FirstOrDefaultAsync();
        if (agreedMonthlyRent is not > 0m)
        {
            return false;
        }
        booking.AgreedMonthlyRent = agreedMonthlyRent.Value;
        booking.DepositAmount = agreedMonthlyRent.Value;

        await _context.Bookings.AddAsync(booking);
        await _context.SaveChangesAsync();
        await _context.Notifications.AddAsync(notificationFactory(booking.BookingId));
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public Task<List<BookingReadModel>> GetAllAsync(int? customerId, int? managerId, string? status)
    {
        return InScope(customerId, managerId)
            .Where(booking => status == null || booking.Status == status)
            .OrderByDescending(booking => booking.BookingId)
            .Select(ToReadModel)
            .ToListAsync();
    }

    public Task<BookingReadModel?> GetByIdAsync(int bookingId, int? customerId, int? managerId)
    {
        return InScope(customerId, managerId)
            .Where(booking => booking.BookingId == bookingId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync();
    }

    public Task<bool> IsPropertyOwnerAsync(int propertyId, int adminId)
    {
        return _context.Properties.AnyAsync(property =>
            property.PropertyId == propertyId && property.OwnerId == adminId);
    }

    public Task<bool> ExistsAsync(int bookingId)
    {
        return _context.Bookings.AnyAsync(booking => booking.BookingId == bookingId);
    }

    public Task<BookingPaymentReadModel?> GetForPaymentAsync(int bookingId)
    {
        return _context.Bookings
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
    }

    public async Task<bool> MarkDepositedAsync(
        int bookingId, DateTimeOffset asOfUtc, DateTime holdUntilUtc, Payments payment, Notifications notification)
    {
        var instantUtc = asOfUtc.UtcDateTime;
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await BookingPaymentLock.LockAsync(_context, bookingId);
        if (await _context.PaymentRequests.AnyAsync(item => item.BookingId == bookingId &&
            (item.Status == PaymentRequestStatuses.Creating || item.Status == PaymentRequestStatuses.Pending || item.Status == PaymentRequestStatuses.RequiresReview)))
            return false;

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

    public async Task<bool> CancelAsync(
        int bookingId,
        string expectedStatus,
        Notifications? customerNotification,
        DateTimeOffset asOfUtc,
        int? managerId = null)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await BookingPaymentLock.LockAsync(_context, bookingId);
        // P6-P10 will provide the refund workflow. Until then, never bypass a real deposit or an uncertain checkout.
        //NGHIA
        if (await _context.PaymentRequests.AnyAsync(item => item.BookingId == bookingId &&
            (item.Status == PaymentRequestStatuses.Creating || item.Status == PaymentRequestStatuses.Pending || item.Status == PaymentRequestStatuses.RequiresReview)))
        { return false; }
        if (expectedStatus == BookingStatuses.Deposited)
        {
            // OD07: re-check the 7-day hold and refund requests under the lock; the deposit is kept.
            var hold = await _context.Bookings
                .Where(booking => booking.BookingId == bookingId)
                .Select(booking => new
                {
                    HasOpenRefund = booking.RefundRequests.Any(refund => refund.Status != BookingRefundRequestStatuses.Rejected),
                    DepositPaidAtUtc = booking.Payments
                        .Where(payment => payment.Status == PaymentStatuses.Success && payment.InvoiceId == null &&
                            payment.ContractId == null && payment.ReadingId == null)
                        .OrderBy(payment => payment.PaymentId)
                        .Select(payment => payment.PaidAtUtc)
                        .FirstOrDefault()
                })
                .SingleOrDefaultAsync();
            if (hold is null || hold.HasOpenRefund || !DepositHoldPolicy.IsHoldOver(hold.DepositPaidAtUtc, asOfUtc.UtcDateTime))
            { return false; }
        }
        else if (await _context.Payments.AnyAsync(item => item.BookingId == bookingId && item.PaymentMethod == PaymentMethods.PayOS && item.Status == PaymentStatuses.Success))
        { return false; }
        //NGHIA
        if (_context.Database.IsNpgsql())
        {
            // Tạo hợp đồng cũng khoá dòng booking này, nên huỷ và lập hợp đồng không thể chạy chen nhau.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Bookings\" WHERE \"BookingId\" = {bookingId} FOR UPDATE");
            if (managerId.HasValue)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT assignment.\"PropertyId\" FROM \"PropertyManagerAssignments\" assignment JOIN \"Rooms\" room ON room.\"PropertyId\" = assignment.\"PropertyId\" JOIN \"Bookings\" booking ON booking.\"RoomId\" = room.\"RoomId\" WHERE booking.\"BookingId\" = {bookingId} AND assignment.\"ManagerId\" = {managerId.Value} FOR SHARE OF assignment");
            }
        }

        if (managerId.HasValue)
        {
            var propertyId = await _context.Bookings
                .Where(booking => booking.BookingId == bookingId)
                .Select(booking => (int?)booking.Room!.PropertyId)
                .FirstOrDefaultAsync();
            var hasActiveAssignment = propertyId.HasValue &&
                await _context.PropertyManagerAssignments.AnyAsync(assignment =>
                    assignment.PropertyId == propertyId.Value &&
                    assignment.ManagerId == managerId.Value &&
                    assignment.Manager!.IsActive &&
                    assignment.Manager.Role!.RoleName == RoleNames.Manager);
            if (!hasActiveAssignment)
            {
                return false;
            }
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
    private IQueryable<Bookings> InScope(int? customerId, int? managerId)
    {
        return _context.Bookings
            .AsNoTracking()
            .Where(booking => customerId == null || booking.CustomerId == customerId)
            .Where(booking => managerId == null || booking.Room!.Property!.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == RoleNames.Manager));
    }

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
        booking.Contract != null ? (int?)booking.Contract.ContractId : null,
        booking.RefundRequests.OrderByDescending(refund => refund.CreatedAtUtc).ThenByDescending(refund => refund.BookingRefundRequestId).Select(refund => refund.Status).FirstOrDefault(),
        booking.Status == BookingStatuses.Deposited && booking.Contract == null &&
            !booking.RefundRequests.Any(refund => refund.Status != BookingRefundRequestStatuses.Rejected) &&
            booking.Payments.Count(payment => payment.Status == PaymentStatuses.Success) == 1 &&
            booking.Payments.Any(payment => payment.Status == PaymentStatuses.Success && payment.PaymentMethod == PaymentMethods.PayOS &&
                payment.UserId == booking.CustomerId && payment.Amount == booking.DepositAmount && payment.InvoiceId == null && payment.ContractId == null && payment.ReadingId == null &&
                payment.PaymentRequest != null && payment.PaymentRequest.Provider == "PayOS" && payment.PaymentRequest.Status == PaymentRequestStatuses.Succeeded &&
                payment.PaymentRequest.Currency == "VND" && payment.PaymentRequest.Amount == payment.Amount && payment.PaymentRequest.CustomerId == booking.CustomerId &&
                payment.PaymentRequest.BookingId == booking.BookingId && payment.PaymentRequest.InvoiceId == null) &&
            !booking.PaymentRequests.Any(request => request.Status == PaymentRequestStatuses.Creating || request.Status == PaymentRequestStatuses.Pending || request.Status == PaymentRequestStatuses.RequiresReview),
        booking.PaymentRequests.OrderByDescending(request => request.CreatedAtUtc).ThenByDescending(request => request.PaymentRequestId).Select(request => (int?)request.PaymentRequestId).FirstOrDefault(),
        booking.Payments
            .Where(payment => payment.Status == PaymentStatuses.Success && payment.InvoiceId == null &&
                payment.ContractId == null && payment.ReadingId == null)
            .OrderBy(payment => payment.PaymentId)
            .Select(payment => payment.PaidAtUtc)
            .FirstOrDefault(),
        booking.AgreedMonthlyRent);
}
