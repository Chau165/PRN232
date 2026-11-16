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
                room.PropertyRoomType!.MonthlyRent,
                room.MaxOccupants, room.PropertyId, room.RoomTypeId))
            .FirstOrDefaultAsync();
    }

    public Task<BookingRoomReadModel?> GetRoomTypeAsync(int propertyId, int roomTypeId)
        => _context.PropertyRoomTypes.AsNoTracking()
            .Where(type => type.PropertyId == propertyId && type.RoomTypeId == roomTypeId && type.IsActive)
            .Select(type => new BookingRoomReadModel(null, type.RoomType!.TypeName, type.Property!.OwnerId,
                type.MonthlyRent, type.Rooms.Where(room => room.Status == RoomStatuses.Available)
                    .Select(room => (int?)room.MaxOccupants).Max() ?? 1, type.PropertyId, type.RoomTypeId))
            .SingleOrDefaultAsync();

    public Task<List<PRN232.Application.DTOs.EligibleContractRoomDto>> GetEligibleContractRoomsAsync(int bookingId, DateTimeOffset asOfUtc)
        => RoomInventoryQueries.EligibleRoomsAsync(_context, bookingId, asOfUtc);

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

    public async Task<BookingAddResult> AddIfRoomRentableAsync(
        Bookings booking, Func<int, Notifications> notificationFactory, DateTimeOffset asOfUtc)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Old direct-room callers can omit the type identifiers; derive them without changing a supplied location.
        if (booking.RoomId.HasValue && (booking.PropertyId == 0 || booking.RoomTypeId == 0))
        {
            var room = await _context.Rooms.AsNoTracking().SingleOrDefaultAsync(item => item.RoomId == booking.RoomId);
            if (room is null) return BookingAddResult.RoomUnavailable;
            if (booking.PropertyId == 0) booking.PropertyId = room.PropertyId;
            if (booking.RoomTypeId == 0) booking.RoomTypeId = room.RoomTypeId;
        }
        await RoomInventoryQueries.LockAsync(_context, booking.PropertyId, booking.RoomTypeId);
        if (_context.Database.IsNpgsql())
        {
            // Inventory/type and rooms are locked before optional viewing metadata and the customer.
            if (booking.ViewingId.HasValue)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"ViewingSchedules\" WHERE \"ViewingId\" = {booking.ViewingId.Value} FOR UPDATE");
            }
            // B4: lock the customer last, so two parallel requests for different rooms by the
            // same customer are serialized and the second one sees the first Pending booking.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Users\" WHERE \"UserId\" = {booking.CustomerId} FOR UPDATE");
        }

        // B4: one live unpaid booking per customer. Expired Pending holds no longer lock a room
        // (same rule as RoomRentabilityPolicy), and Deposited/Cancelled bookings do not count.
        var instantUtc = asOfUtc.UtcDateTime;
        var pendingBookingId = await _context.Bookings
            .Where(item => item.CustomerId == booking.CustomerId &&
                item.Status == BookingStatuses.Pending &&
                item.ExpiryDate > instantUtc)
            .OrderBy(item => item.BookingId)
            .Select(item => (int?)item.BookingId)
            .FirstOrDefaultAsync();
        if (pendingBookingId is not null)
        {
            return new BookingAddResult(BookingAddOutcome.CustomerHasPendingBooking, pendingBookingId);
        }

        if (booking.ViewingId.HasValue)
        {
            var viewingIsValid = await _context.ViewingSchedules.AnyAsync(viewing =>
                viewing.ViewingId == booking.ViewingId.Value &&
                viewing.CustomerId == booking.CustomerId &&
                viewing.PropertyId == booking.PropertyId && viewing.RoomTypeId == booking.RoomTypeId &&
                (booking.RoomId == null || viewing.RoomId == booking.RoomId) &&
                viewing.Status == ViewingStatuses.Approved);
            var alreadyBookedFromViewing = await _context.Bookings.AnyAsync(item => item.ViewingId == booking.ViewingId);
            if (!viewingIsValid || alreadyBookedFromViewing)
            {
                return BookingAddResult.RoomUnavailable;
            }
        }

        if (!await RoomInventoryQueries.CanReserveAsync(_context, booking.PropertyId, booking.RoomTypeId,
                booking.OccupantCount ?? 1, booking.RoomId, null, asOfUtc)) return BookingAddResult.RoomUnavailable;

        var agreedMonthlyRent = await _context.PropertyRoomTypes
            .Where(type => type.PropertyId == booking.PropertyId && type.RoomTypeId == booking.RoomTypeId && type.IsActive)
            .Select(type => (decimal?)type.MonthlyRent).SingleOrDefaultAsync();
        if (agreedMonthlyRent is not > 0m)
        {
            return BookingAddResult.RoomUnavailable;
        }
        booking.AgreedMonthlyRent = agreedMonthlyRent.Value;
        booking.DepositAmount = agreedMonthlyRent.Value;

        await _context.Bookings.AddAsync(booking);
        await _context.SaveChangesAsync();
        await _context.Notifications.AddAsync(notificationFactory(booking.BookingId));
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return BookingAddResult.Added;
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
            booking.Property!.OwnerId,
            booking.Room != null ? booking.Room.RoomCode : booking.PropertyRoomType!.RoomType!.TypeName,
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

        var currentRoomId = await _context.Bookings.Where(item => item.BookingId == bookingId).Select(item => item.RoomId).SingleOrDefaultAsync();
        if (!await BookingPaymentLock.RoomCanAcceptDepositAsync(_context, currentRoomId, bookingId, instantUtc, default)) return false;
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
        string reason,
        IReadOnlyList<Notifications> notifications,
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
            // Rebuild 3.2: ai hủy và khi nào do BookingService quyết định (lý do hủy). Ở đây chỉ chặn booking
            // còn yêu cầu hoàn cọc cũ đang xử lý, để không hủy chồng lên luồng hoàn cọc payOS.
            var hasOpenRefund = await _context.Bookings
                .Where(booking => booking.BookingId == bookingId)
                .Select(booking => (bool?)booking.RefundRequests.Any(refund => refund.Status != BookingRefundRequestStatuses.Rejected))
                .SingleOrDefaultAsync();
            if (hasOpenRefund is not false)
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
                    $"SELECT assignment.\"PropertyId\" FROM \"PropertyManagerAssignments\" assignment JOIN \"Bookings\" booking ON booking.\"PropertyId\" = assignment.\"PropertyId\" WHERE booking.\"BookingId\" = {bookingId} AND assignment.\"ManagerId\" = {managerId.Value} FOR SHARE OF assignment");
            }
        }

        if (managerId.HasValue)
        {
            var propertyId = await _context.Bookings
                .Where(booking => booking.BookingId == bookingId)
                .Select(booking => (int?)booking.PropertyId)
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
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(booking => booking.Status, BookingStatuses.Cancelled)
                .SetProperty(booking => booking.CancellationReason, reason)
                .SetProperty(booking => booking.CancelledAtUtc, asOfUtc.UtcDateTime));
        if (updated == 0)
        {
            return false;
        }

        if (notifications.Count > 0)
        {
            await _context.Notifications.AddRangeAsync(notifications);
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
            .Where(booking => managerId == null || booking.Property!.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == RoleNames.Manager));
    }

    private static readonly Expression<Func<Bookings, BookingReadModel>> ToReadModel = booking => new BookingReadModel(
        booking.BookingId,
        booking.RoomId,
        booking.Room != null ? booking.Room.RoomCode : "",
        booking.PropertyId,
        booking.Property!.PropertyName,
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
        booking.AgreedMonthlyRent,
        booking.MoveInDate,
        booking.OccupantCount,
        booking.CancellationReason,
        booking.CancelledAtUtc,
        booking.Payments.Any(payment => payment.Status == PaymentStatuses.Success && payment.InvoiceId == null &&
            payment.ContractId == null && payment.ReadingId == null),
        booking.DepositReturnedAtUtc,
        booking.DepositReturnedBy != null ? booking.DepositReturnedBy.FullName : null,
        booking.DepositAppliesToFirstMonth,
        booking.Room != null ? booking.Room.MaxOccupants : booking.PropertyRoomType!.Rooms.Select(room => (int?)room.MaxOccupants).Max() ?? 1,
        booking.RoomTypeId,
        booking.PropertyRoomType!.RoomType!.TypeName);

    public async Task<bool> ConfirmDepositReturnedAsync(
        int bookingId, int confirmedById, DateTimeOffset asOfUtc, Notifications customerNotification)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await BookingPaymentLock.LockAsync(_context, bookingId);
        // Điều kiện "chưa xác nhận" nằm trong câu UPDATE: bấm hai lần thì lần sau khớp 0 dòng.
        var updated = await _context.Bookings
            .Where(booking => booking.BookingId == bookingId &&
                booking.Status == BookingStatuses.Cancelled &&
                booking.CancellationReason == BookingCancellationReasons.LandlordCancelled &&
                booking.DepositReturnedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(booking => booking.DepositReturnedAtUtc, asOfUtc.UtcDateTime)
                .SetProperty(booking => booking.DepositReturnedById, confirmedById));
        if (updated == 0)
        {
            return false;
        }

        await _context.Notifications.AddAsync(customerNotification);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<List<BookingContactReadModel>> GetPropertyContactsAsync(IReadOnlyCollection<int> propertyIds)
    {
        var ids = propertyIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var managers = await _context.PropertyManagerAssignments
            .AsNoTracking()
            .Where(assignment => ids.Contains(assignment.PropertyId) &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == RoleNames.Manager)
            .OrderBy(assignment => assignment.ManagerId)
            .Select(assignment => new BookingContactReadModel(assignment.PropertyId, assignment.Manager!.FullName,
                assignment.Manager.PhoneNumber, assignment.Manager.Email, RoleNames.Manager, assignment.ManagerId))
            .ToListAsync();

        // Khu trọ chưa có quản lý thì khách liên hệ chủ trọ.
        var withoutManager = ids.Except(managers.Select(contact => contact.PropertyId)).ToArray();
        var owners = await _context.Properties
            .AsNoTracking()
            .Where(property => withoutManager.Contains(property.PropertyId))
            .Select(property => new BookingContactReadModel(property.PropertyId, property.Owner!.FullName,
                property.Owner.PhoneNumber, property.Owner.Email, "Owner", property.OwnerId))
            .ToListAsync();
        return [.. managers, .. owners];
    }
}
