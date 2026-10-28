using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class BookingService : IBookingService
{
    // Thời gian giữ chỗ để khách hoàn tất thanh toán cọc; quá hạn thì phòng tự được nhả (D3).
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);

    private readonly IBookingRepository _repository;
    private readonly TimeProvider _timeProvider;

    public BookingService(IBookingRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<BookingDto> CreateAsync(CreateBookingDto dto, int customerId)
    {
        var room = await _repository.GetRoomAsync(dto.RoomId)
            ?? throw new BookingNotFoundException("Room does not exist.");

        var now = _timeProvider.GetUtcNow();
        var booking = new Bookings
        {
            RoomId = room.RoomId,
            CustomerId = customerId,
            DepositAmount = room.MonthlyRent,
            Status = BookingStatuses.Pending,
            ExpiryDate = now.Add(PaymentWindow).UtcDateTime
        };

        if (!await _repository.AddIfRoomRentableAsync(booking, now))
        {
            throw new BookingRoomUnavailableException("Room is not available for booking.");
        }

        return await _repository.GetByIdAsync(booking.BookingId, customerId, null)
            ?? throw new InvalidOperationException("Created booking was not found.");
    }

    public async Task<BookingDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, ownerId) = GetScope(userId, role);
        var booking = await _repository.GetByIdAsync(id, customerId, ownerId);
        if (booking is null && (customerId.HasValue || ownerId.HasValue) && await _repository.ExistsAsync(id))
        {
            throw new BookingAuthorizationException("You cannot access this booking.");
        }
        return booking;
    }

    // Customer chỉ thấy booking của mình, Manager chỉ thấy booking thuộc khu trọ mình sở hữu, Admin thấy tất cả.
    private static (int? CustomerId, int? OwnerId) GetScope(int userId, string role) => role switch
    {
        RoleNames.Customer => (userId, null),
        RoleNames.Manager => (null, userId),
        RoleNames.Admin => (null, null),
        _ => throw new BookingAuthorizationException("Your role cannot access bookings.")
    };
}

public sealed class BookingNotFoundException(string message) : Exception(message);

public sealed class BookingRoomUnavailableException(string message) : Exception(message);

public sealed class BookingAuthorizationException(string message) : Exception(message);
