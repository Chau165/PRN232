using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IBookingRepository
{
    Task<BookingRoomDto?> GetRoomAsync(int roomId);
    Task<bool> AddIfRoomRentableAsync(Bookings booking, DateTimeOffset asOfUtc);
    Task<BookingDto?> GetByIdAsync(int bookingId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int bookingId);
    Task<BookingPaymentDto?> GetForPaymentAsync(int bookingId);
    Task<bool> MarkDepositedAsync(
        int bookingId, DateTimeOffset asOfUtc, DateTime holdUntilUtc, Payments payment, Notifications notification);
}
