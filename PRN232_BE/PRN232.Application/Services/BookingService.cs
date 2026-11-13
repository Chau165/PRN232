using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PRN232.Application.Services;

public class BookingService : IBookingService
{
    // Thời gian giữ chỗ để khách hoàn tất thanh toán cọc; quá hạn thì phòng tự được nhả (D3).
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);

    // Legacy mock display timestamp only; Deposited holds the room indefinitely in RoomRentabilityPolicy.
    // payOS settlement keeps the original checkout deadline and never uses this legacy duration.
    public static readonly TimeSpan DepositHoldPeriod = TimeSpan.FromDays(7);

    private readonly IBookingRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly TimeProvider _timeProvider;
    private readonly INotificationRealtimePublisher? _realtimePublisher;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository repository,
        IPaymentGateway paymentGateway,
        TimeProvider timeProvider,
        INotificationRealtimePublisher? realtimePublisher = null,
        ILogger<BookingService>? logger = null)
    {
        _repository = repository;
        _paymentGateway = paymentGateway;
        _timeProvider = timeProvider;
        _realtimePublisher = realtimePublisher;
        _logger = logger ?? NullLogger<BookingService>.Instance;
    }

    public async Task<BookingDto> CreateAsync(CreateBookingDto dto, int customerId)
    {
        var room = await _repository.GetRoomAsync(dto.RoomId);
        if (room is null)
        {
            throw new BookingNotFoundException("Room does not exist.");
        }

        var now = _timeProvider.GetUtcNow();
        var booking = CreateBookingEntity(room, customerId, now);
        Notifications? pendingNotification = null;

        if (!await _repository.AddIfRoomRentableAsync(booking, bookingId => pendingNotification = new Notifications
            {
                UserId = room.OwnerId,
                Title = $"Booking #{bookingId} - Phòng {room.RoomCode} đang chờ thanh toán cọc",
                Type = NotificationTypes.Booking,
                IsRead = false
            }, now))
        {
            throw new BookingRoomUnavailableException("Room is not available for booking.");
        }
        var created = await _repository.GetByIdAsync(booking.BookingId, customerId, null);
        if (created is null)
        {
            throw new InvalidOperationException("Created booking was not found.");
        }
        if (pendingNotification is not null && _realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync([pendingNotification]);
        }
        return ConvertToDto(created);
    }

    public async Task<List<BookingDto>> GetAllAsync(int userId, string role, string? status)
    {
        if (status is not null &&
            status is not (BookingStatuses.Pending or BookingStatuses.Deposited or BookingStatuses.Cancelled))
        {
            throw new BookingBusinessRuleException("Status must be Pending, Deposited, or Cancelled.");
        }

        var (customerId, managerId) = GetScope(userId, role);
        var bookings = await _repository.GetAllAsync(customerId, managerId, status);
        return bookings.Select(ConvertToDto).ToList();
    }

    public async Task<BookingDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, managerId) = GetScope(userId, role);
        var booking = await _repository.GetByIdAsync(id, customerId, managerId);
        if (booking is null && (customerId.HasValue || managerId.HasValue) && await _repository.ExistsAsync(id))
        {
            throw new BookingAuthorizationException("You cannot access this booking.");
        }
        if (booking is null)
        {
            return null;
        }
        return ConvertToDto(booking);
    }

    public async Task<BookingDto> PayAsync(int id, PayBookingDto dto, int customerId)
    {
        if (!PaymentMethods.IsValid(dto.PaymentMethod))
        {
            throw new BookingBusinessRuleException("Payment method must be VNPay or MoMo.");
        }

        var booking = await _repository.GetForPaymentAsync(id);
        if (booking is null)
        {
            throw new BookingNotFoundException("Booking does not exist.");
        }
        if (booking.CustomerId != customerId)
        {
            throw new BookingAuthorizationException("You cannot pay for another customer's booking.");
        }

        var now = _timeProvider.GetUtcNow();
        if (booking.Status != BookingStatuses.Pending)
        {
            throw new BookingStatusConflictException("Only pending bookings can be paid.");
        }
        if (booking.ExpiryDate <= now.UtcDateTime)
        {
            throw new BookingStatusConflictException("The payment window for this booking has expired.");
        }

        if (!await _paymentGateway.ChargeAsync(dto.PaymentMethod, booking.DepositAmount))
        {
            _logger.LogWarning("Deposit payment declined for booking {BookingId} by customer {CustomerId} via {PaymentMethod}.",
                id, customerId, dto.PaymentMethod);
            throw new BookingPaymentFailedException("The payment was declined.");
        }

        var payment = new Payments
        {
            UserId = customerId,
            BookingId = id,
            Amount = booking.DepositAmount,
            PaidAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
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
            // Đã trừ tiền nhưng không ghi nhận được (một lần thanh toán khác vừa xong trước): cần đối soát.
            _logger.LogError("Deposit for booking {BookingId} was charged ({Amount} via {PaymentMethod}) but the booking was no longer pending; reconcile this payment.",
                id, booking.DepositAmount, dto.PaymentMethod);
            throw new BookingStatusConflictException("Only pending bookings can be paid.");
        }
        _logger.LogInformation("Booking {BookingId} deposit paid by customer {CustomerId}: {Amount} via {PaymentMethod}.",
            id, customerId, booking.DepositAmount, dto.PaymentMethod);
        var paid = await _repository.GetByIdAsync(id, customerId, null);
        if (paid is null)
        {
            throw new InvalidOperationException("Paid booking was not found.");
        }
        if (_realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync([notification]);
        }
        return ConvertToDto(paid);
    }

    public async Task<BookingDto> CancelAsync(int id, int userId, string role)
    {
        var booking = await GetByIdAsync(id, userId, role);
        if (booking is null)
        {
            throw new BookingNotFoundException("Booking does not exist.");
        }
        if (booking.Status == BookingStatuses.Cancelled)
        {
            throw new BookingStatusConflictException("Booking is already cancelled.");
        }
        if (booking.ContractId is not null)
        {
            throw new BookingStatusConflictException("This booking already has a contract and cannot be cancelled.");
        }

        if (role == RoleNames.Admin &&
            !await _repository.IsPropertyOwnerAsync(booking.PropertyId, userId))
        {
            throw new BookingAuthorizationException("Only the property owner can cancel this booking as an Admin.");
        }
        // Khách chỉ tự huỷ được khi chưa trả cọc; booking đã cọc phải do quản lý/admin huỷ
        // (hoàn cọc xử lý ngoài hệ thống vì thanh toán là giả lập).

        if (role == RoleNames.Customer && booking.Status != BookingStatuses.Pending)
        {
            throw new BookingStatusConflictException("Deposited bookings require a cancellation/refund request.");
        }

        Notifications? notification = null;
        if (role != RoleNames.Customer)
        {
            notification = new Notifications
            {
                UserId = booking.CustomerId,
                Title = $"Booking #{id} - Phòng {booking.RoomCode} đã bị huỷ",
                Type = NotificationTypes.Booking,
                IsRead = false
            };
        }
        int? managerId = role == RoleNames.Manager ? userId : null;
        if (!await _repository.CancelAsync(id, booking.Status, notification, managerId))
        {
            throw new BookingStatusConflictException("Booking status has changed. Please reload and try again.");
        }
        var cancelled = await _repository.GetByIdAsync(id, null, null);
        if (cancelled is null)
        {
            throw new InvalidOperationException("Cancelled booking was not found.");
        }
        if (notification is not null && _realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync([notification]);
        }
        return ConvertToDto(cancelled);
    }

    private static Bookings CreateBookingEntity(BookingRoomReadModel room, int customerId, DateTimeOffset now)
    {
        return new Bookings
        {
            RoomId = room.RoomId,
            CustomerId = customerId,
            DepositAmount = room.MonthlyRent,
            Status = BookingStatuses.Pending,
            ExpiryDate = now.Add(PaymentWindow).UtcDateTime
        };
    }

    private static (int? CustomerId, int? ManagerId) GetScope(int userId, string role)
    {
        var scope = AccessScope.For(userId, role);
        if (scope is null)
        {
            throw new BookingAuthorizationException("Your role cannot access bookings.");
        }
        return scope.Value;
    }

    private static BookingDto ConvertToDto(BookingReadModel booking)
    {
        return new BookingDto(
            booking.BookingId,
            booking.RoomId,
            booking.RoomCode,
            booking.PropertyId,
            booking.PropertyName,
            booking.CustomerId,
            booking.CustomerName,
            booking.DepositAmount,
            booking.Status,
            UtcTimestamp.ForResponse(booking.ExpiryDate),
            booking.CustomerPhone,
            booking.ContractId, booking.RefundStatus, booking.CanRequestRefund, booking.LatestPaymentRequestId);
    }
}

public sealed class BookingNotFoundException(string message) : Exception(message);

public sealed class BookingRoomUnavailableException(string message) : Exception(message);

public sealed class BookingAuthorizationException(string message) : Exception(message);

public sealed class BookingBusinessRuleException(string message) : Exception(message);

public sealed class BookingStatusConflictException(string message) : Exception(message);

public sealed class BookingPaymentFailedException(string message) : Exception(message);
