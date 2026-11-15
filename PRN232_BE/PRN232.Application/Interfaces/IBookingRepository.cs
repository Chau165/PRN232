using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IBookingRepository
{
    Task<BookingRoomReadModel?> GetRoomAsync(int roomId);
    Task<ViewingBookingReadModel?> GetViewingForBookingAsync(int viewingId, int customerId);
    // Adds the booking only if the room is rentable and the customer has no live unpaid booking
    // (B4); both checks run in one transaction under row locks.
    Task<BookingAddResult> AddIfRoomRentableAsync(Bookings booking, Func<int, Notifications> notificationFactory, DateTimeOffset asOfUtc);
    Task<List<BookingReadModel>> GetAllAsync(int? customerId, int? ownerId, string? status);
    Task<BookingReadModel?> GetByIdAsync(int bookingId, int? customerId, int? ownerId);
    Task<bool> IsPropertyOwnerAsync(int propertyId, int adminId);
    Task<bool> ExistsAsync(int bookingId);
    Task<BookingPaymentReadModel?> GetForPaymentAsync(int bookingId);
    Task<bool> MarkDepositedAsync(
        int bookingId, DateTimeOffset asOfUtc, DateTime holdUntilUtc, Payments payment, Notifications notification);
    Task<bool> CancelAsync(
        int bookingId,
        string expectedStatus,
        string reason,
        IReadOnlyList<Notifications> notifications,
        DateTimeOffset asOfUtc,
        int? managerId = null);
    Task<List<BookingContactReadModel>> GetPropertyContactsAsync(IReadOnlyCollection<int> propertyIds);
    // Chỉ ghi khi booking vẫn đang nợ khách tiền cọc (chủ trọ hủy, đã cọc, chưa xác nhận).
    Task<bool> ConfirmDepositReturnedAsync(int bookingId, int confirmedById, DateTimeOffset asOfUtc, Notifications customerNotification);
}

public enum BookingAddOutcome
{
    Added,
    RoomUnavailable,
    // B4: the customer already holds a Pending booking that has not expired.
    CustomerHasPendingBooking
}

public sealed record BookingAddResult(BookingAddOutcome Outcome, int? PendingBookingId = null)
{
    public static BookingAddResult Added { get; } = new(BookingAddOutcome.Added);
    public static BookingAddResult RoomUnavailable { get; } = new(BookingAddOutcome.RoomUnavailable);
}
