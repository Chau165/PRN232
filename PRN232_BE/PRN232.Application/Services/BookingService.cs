using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class BookingService : IBookingService
{
    // Thời gian giữ chỗ để khách hoàn tất thanh toán cọc; quá hạn thì phòng tự được nhả (D3).
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);

    // Sau khi cọc, phòng được giữ tiếp để quản lý lập hợp đồng; quá hạn mà chưa có hợp đồng thì nhả phòng (D2).
    public static readonly TimeSpan DepositHoldPeriod = TimeSpan.FromDays(7);

    private readonly IBookingRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly TimeProvider _timeProvider;

    public BookingService(IBookingRepository repository, IPaymentGateway paymentGateway, TimeProvider timeProvider)
    {
        _repository = repository;
        _paymentGateway = paymentGateway;
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

    public async Task<BookingDto> PayAsync(int id, PayBookingDto dto, int customerId)
    {
        if (!PaymentMethods.IsValid(dto.PaymentMethod))
        {
            throw new BookingBusinessRuleException("Payment method must be VNPay or MoMo.");
        }

        var booking = await _repository.GetForPaymentAsync(id)
            ?? throw new BookingNotFoundException("Booking does not exist.");
        if (booking.CustomerId != customerId)
        {
            throw new BookingAuthorizationException("You cannot pay for another customer's booking.");
        }

        var now = _timeProvider.GetUtcNow();
        if (booking.Status != BookingStatuses.Pending)
        {
            throw new BookingNotPayableException("Only pending bookings can be paid.");
        }
        if (booking.ExpiryDate <= now.UtcDateTime)
        {
            throw new BookingNotPayableException("The payment window for this booking has expired.");
        }

        if (!await _paymentGateway.ChargeAsync(dto.PaymentMethod, booking.DepositAmount))
        {
            throw new BookingPaymentFailedException("The payment was declined.");
        }

        var payment = new Payments
        {
            UserId = customerId,
            BookingId = id,
            Amount = booking.DepositAmount,
            PaymentMethod = dto.PaymentMethod,
            Status = PaymentStatuses.Success
        };
        var notification = new Notifications
        {
            UserId = booking.OwnerId,
            Title = $"Booking #{id} - Phòng {booking.RoomCode} đã được đặt cọc",
            Type = NotificationTypes.Booking,
            IsRead = false
        };

        // Repository kiểm tra lại Pending + còn hạn ngay lúc ghi, vì giữa lúc đọc ở trên và lúc ghi
        // có thể đã có một lần bấm thanh toán khác chạy xong trước.
        if (!await _repository.MarkDepositedAsync(id, now, now.Add(DepositHoldPeriod).UtcDateTime, payment, notification))
        {
            throw new BookingNotPayableException("Only pending bookings can be paid.");
        }

        return await _repository.GetByIdAsync(id, customerId, null)
            ?? throw new InvalidOperationException("Paid booking was not found.");
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

public sealed class BookingBusinessRuleException(string message) : Exception(message);

public sealed class BookingNotPayableException(string message) : Exception(message);

public sealed class BookingPaymentFailedException(string message) : Exception(message);
