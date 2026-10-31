using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IBookingRepository
{
    Task<BookingRoomReadModel?> GetRoomAsync(int roomId);
    Task<bool> AddIfRoomRentableAsync(Bookings booking, DateTimeOffset asOfUtc);
    Task<List<BookingReadModel>> GetAllAsync(int? customerId, int? ownerId, string? status);
    Task<BookingReadModel?> GetByIdAsync(int bookingId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int bookingId);
    Task<BookingPaymentReadModel?> GetForPaymentAsync(int bookingId);
    Task<bool> MarkDepositedAsync(
        int bookingId, DateTimeOffset asOfUtc, DateTime holdUntilUtc, Payments payment, Notifications notification);
    Task<bool> CancelAsync(int bookingId, string expectedStatus, Notifications? customerNotification);
}
