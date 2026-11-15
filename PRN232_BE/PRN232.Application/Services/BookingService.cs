using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.Settings;
using PRN232.Application.States;
using PRN232.Application.Validation;
using PRN232.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PRN232.Application.Services;

public class BookingService : IBookingService
{
    // Thời gian giữ chỗ để khách hoàn tất thanh toán cọc; quá hạn thì phòng tự được nhả (D3).
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);

    // Legacy mock display timestamp only: ExpiryDate does not end a Deposited hold. The hold rule
    // (deposit time + 7 days, see DepositHoldPolicy) is read from the deposit payment's PaidAtUtc.
    // payOS settlement keeps the original checkout deadline and never uses this legacy duration.
    public static readonly TimeSpan DepositHoldPeriod = TimeSpan.FromDays(7);

    private readonly IBookingRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly TimeProvider _timeProvider;
    private readonly INotificationRealtimePublisher? _realtimePublisher;
    private readonly ILogger<BookingService> _logger;
    private readonly IUserContactLookup? _contactLookup;
    private readonly BookingRefundSettings _refundSettings;
    private readonly BookingDepositSettings _depositSettings;

    public BookingService(
        IBookingRepository repository,
        IPaymentGateway paymentGateway,
        TimeProvider timeProvider,
        INotificationRealtimePublisher? realtimePublisher = null,
        ILogger<BookingService>? logger = null,
        IUserContactLookup? contactLookup = null,
        BookingRefundSettings? refundSettings = null,
        BookingDepositSettings? depositSettings = null)
    {
        _contactLookup = contactLookup;
        _refundSettings = refundSettings ?? new BookingRefundSettings();
        _depositSettings = depositSettings ?? new BookingDepositSettings();
        _repository = repository;
        _paymentGateway = paymentGateway;
        _timeProvider = timeProvider;
        _realtimePublisher = realtimePublisher;
        _logger = logger ?? NullLogger<BookingService>.Instance;
    }

    public async Task<BookingDto> CreateAsync(CreateBookingDto dto, int customerId)
    {
        // The deposit hold starts here; the owner must be able to call the customer.
        await PhoneNumberRequiredException.EnsureAsync(
            _contactLookup, customerId, PhoneNumberRequiredException.SelfMessage);

        var room = await _repository.GetRoomAsync(dto.RoomId);
        if (room is null)
        {
            throw new BookingNotFoundException("Room does not exist.");
        }

        var occupantCount = dto.OccupantCount ?? 1;
        if (occupantCount > room.MaxOccupants)
        {
            throw new BookingOccupancyExceededException(BookingRules.OccupancyExceeded(room.RoomCode, room.MaxOccupants));
        }

        if (dto.ViewingId.HasValue)
        {
            var viewing = await _repository.GetViewingForBookingAsync(dto.ViewingId.Value, customerId);
            if (viewing is null ||
                viewing.Status != ViewingStatuses.Approved ||
                viewing.RoomId != dto.RoomId)
            {
                throw new BookingStatusConflictException(
                    "Booking from a viewing requires your approved viewing and its confirmed physical room.");
            }
        }

        var now = _timeProvider.GetUtcNow();
        var booking = CreateBookingEntity(room, customerId, now, dto.ViewingId);
        booking.MoveInDate = dto.MoveInDate ?? BookingRules.Today(_timeProvider);
        booking.OccupantCount = occupantCount;
        booking.DepositAppliesToFirstMonth = _depositSettings.AppliesToFirstMonth;
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
        return await ToDtoAsync(created);
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
        return await ToDtosAsync(bookings);
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
        return await ToDtoAsync(booking);
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
        return await ToDtoAsync(paid);
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

        var deposited = booking.Status == BookingStatuses.Deposited;
        if (deposited && booking.RefundStatus is not (null or BookingRefundRequestStatuses.Rejected))
        {
            // Yêu cầu hoàn cọc cũ (luồng payOS đã khóa) phải được xử lý xong trước.
            throw new BookingStatusConflictException("Booking này đang có yêu cầu hoàn cọc chưa xử lý xong.");
        }

        // Rebuild 3.2 (PLAN D3 bước 3): khách hủy thì mất cọc; chủ trọ hủy trong thời hạn giữ phòng thì hoàn
        // 100%; hết thời hạn mà khách chưa đến nhận phòng thì quản lý nhả phòng và khách mất cọc.
        var reason = role == RoleNames.Customer
            ? BookingCancellationReasons.CustomerCancelled
            : deposited && booking.CanReleaseRoom
                ? BookingCancellationReasons.NoShow
                : BookingCancellationReasons.LandlordCancelled;
        List<Notifications> notifications = role != RoleNames.Customer
            ? [CancellationNotification(booking, reason)]
            : deposited
                // Rebuild 3.2c: khách tự hủy sau khi cọc thì phòng trống lại, báo người phụ trách khu trọ.
                ? (await _repository.GetPropertyContactsAsync([booking.PropertyId]))
                    .Select(staff => new Notifications
                    {
                        UserId = staff.UserId,
                        Title = $"Booking #{id} - Khách {booking.CustomerName} đã hủy đặt cọc phòng {booking.RoomCode}",
                        Body = "Phòng đã trống và có thể cho người khác thuê. Theo chính sách, khách tự hủy nên tiền cọc không được hoàn.",
                        Type = NotificationTypes.Booking,
                        IsRead = false
                    })
                    .ToList()
                : [];

        int? managerId = role == RoleNames.Manager ? userId : null;
        if (!await _repository.CancelAsync(id, booking.Status, reason, notifications, _timeProvider.GetUtcNow(), managerId))
        {
            throw new BookingStatusConflictException("Booking status has changed. Please reload and try again.");
        }
        var cancelled = await _repository.GetByIdAsync(id, null, null);
        if (cancelled is null)
        {
            throw new InvalidOperationException("Cancelled booking was not found.");
        }
        if (notifications.Count > 0 && _realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync(notifications);
        }
        return await ToDtoAsync(cancelled);
    }

    // Rebuild 3.2b: quản lý/chủ trọ đã trả lại tiền cọc cho khách NGOÀI hệ thống và xác nhận để lưu vết.
    public async Task<BookingDto> ConfirmDepositReturnedAsync(int id, int userId, string role)
    {
        if (role is not (RoleNames.Manager or RoleNames.Admin))
        {
            throw new BookingAuthorizationException("Chỉ quản lý hoặc chủ trọ được xác nhận đã hoàn cọc.");
        }

        var booking = await GetByIdAsync(id, userId, role)
            ?? throw new BookingNotFoundException("Booking does not exist.");
        if (role == RoleNames.Admin && !await _repository.IsPropertyOwnerAsync(booking.PropertyId, userId))
        {
            throw new BookingAuthorizationException("Chỉ chủ trọ của khu này được xác nhận đã hoàn cọc.");
        }
        if (!booking.DepositRefundDue)
        {
            throw new BookingStatusConflictException(booking.DepositReturnedAtUtc is null
                ? "Booking này không có khoản cọc phải hoàn (chỉ hoàn khi chủ trọ hủy sau khi khách đã cọc)."
                : "Khoản cọc của booking này đã được xác nhận hoàn trước đó.");
        }

        var amount = booking.DepositAmount.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
        var notification = new Notifications
        {
            UserId = booking.CustomerId,
            Title = $"Booking #{id} - Đã xác nhận hoàn {amount}đ tiền cọc phòng {booking.RoomCode}",
            Body = $"{(role == RoleNames.Admin ? "Chủ trọ" : "Quản lý")} khu {booking.PropertyName} xác nhận đã chuyển lại cho bạn {amount}đ tiền cọc. Nếu bạn chưa nhận được, hãy liên hệ quản lý khu trọ.",
            Type = NotificationTypes.Booking,
            IsRead = false
        };
        if (!await _repository.ConfirmDepositReturnedAsync(id, userId, _timeProvider.GetUtcNow(), notification))
        {
            throw new BookingStatusConflictException("Khoản cọc của booking này đã được xác nhận hoàn trước đó.");
        }
        if (_realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync([notification]);
        }
        var confirmed = await _repository.GetByIdAsync(id, null, null)
            ?? throw new InvalidOperationException("Confirmed booking was not found.");
        return await ToDtoAsync(confirmed);
    }

    private static Bookings CreateBookingEntity(
        BookingRoomReadModel room,
        int customerId,
        DateTimeOffset now,
        int? viewingId)
    {
        return new Bookings
        {
            RoomId = room.RoomId,
            CustomerId = customerId,
            DepositAmount = room.MonthlyRent,
            AgreedMonthlyRent = room.MonthlyRent,
            ViewingId = viewingId,
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

    private static Notifications CancellationNotification(BookingDto booking, string reason)
    {
        var (title, body) = reason switch
        {
            BookingCancellationReasons.NoShow => (
                $"Booking #{booking.BookingId} - Phòng {booking.RoomCode} đã được giải phóng do quá hạn nhận phòng",
                $"Bạn không nhận phòng trong thời hạn giữ phòng{HoldDeadlineText(booking.HoldUntilUtc)} nên phòng đã được mở cho người khác thuê. Theo chính sách giữ phòng, tiền cọc không được hoàn."),
            _ when booking.Status == BookingStatuses.Deposited => (
                $"Booking #{booking.BookingId} - Phòng {booking.RoomCode} đã bị chủ trọ hủy",
                $"Bạn được hoàn 100% tiền cọc ({booking.DepositAmount.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}đ). Hệ thống không chuyển tiền tự động, vui lòng liên hệ quản lý khu trọ {booking.PropertyName} (xem mục Liên hệ trong booking) để nhận lại tiền."),
            _ => ($"Booking #{booking.BookingId} - Phòng {booking.RoomCode} đã bị huỷ", (string?)null)
        };
        return new Notifications
        {
            UserId = booking.CustomerId,
            Title = title,
            Body = body,
            Type = NotificationTypes.Booking,
            IsRead = false
        };
    }

    // " (đến hết ngày 23/11/2026)" theo giờ VN; HoldUntilUtc là 00:00 ngày hôm sau nên lùi 1 tích tắc.
    private static string HoldDeadlineText(DateTime? holdUntilUtc) =>
        holdUntilUtc is { } until
            ? $" (đến hết ngày {TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(until, DateTimeKind.Utc).AddTicks(-1), VietnamTimeZone):dd/MM/yyyy})"
            : "";

    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private async Task<BookingDto> ToDtoAsync(BookingReadModel booking) => (await ToDtosAsync([booking]))[0];

    private async Task<List<BookingDto>> ToDtosAsync(IReadOnlyList<BookingReadModel> bookings)
    {
        var contacts = (await _repository.GetPropertyContactsAsync(bookings.Select(booking => booking.PropertyId).ToArray()))
            .ToLookup(contact => contact.PropertyId);
        return bookings.Select(booking => ConvertToDto(booking, contacts[booking.PropertyId])).ToList();
    }

    private BookingDto ConvertToDto(BookingReadModel booking, IEnumerable<BookingContactReadModel> contacts)
    {
        var holding = booking.Status == BookingStatuses.Deposited && booking.ContractId is null;
        var holdUntil = holding ? DepositHoldPolicy.HoldUntilUtc(booking.DepositPaidAtUtc, booking.MoveInDate) : null;
        var canReleaseRoom = holding &&
            (booking.RefundStatus is null or BookingRefundRequestStatuses.Rejected) &&
            DepositHoldPolicy.IsHoldOver(booking.DepositPaidAtUtc, _timeProvider.GetUtcNow().UtcDateTime, booking.MoveInDate);
        var depositRefundDue = booking.Status == BookingStatuses.Cancelled &&
            booking.CancellationReason == BookingCancellationReasons.LandlordCancelled && booking.DepositPaid &&
            booking.DepositReturnedAtUtc is null;
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
            booking.ContractId, booking.RefundStatus, booking.CanRequestRefund && _refundSettings.RequestsEnabled,
            booking.LatestPaymentRequestId,
            UtcTimestamp.ForResponse(holdUntil), canReleaseRoom, booking.AgreedMonthlyRent,
            booking.MoveInDate, booking.OccupantCount,
            booking.CancellationReason, UtcTimestamp.ForResponse(booking.CancelledAtUtc), depositRefundDue,
            contacts.Select(contact => new BookingContactDto(contact.FullName, contact.PhoneNumber, contact.Email, contact.Role)).ToArray(),
            UtcTimestamp.ForResponse(booking.DepositReturnedAtUtc), booking.DepositReturnedByName,
            booking.DepositAppliesToFirstMonth);
    }
}

public sealed class BookingNotFoundException(string message) : Exception(message);

public sealed class BookingRoomUnavailableException(string message) : Exception(message);

// Controller trả 400 với lỗi gắn vào field occupantCount, như lỗi FluentValidation.
public sealed class BookingOccupancyExceededException(string message) : Exception(message);

public sealed class BookingAuthorizationException(string message) : Exception(message);

public sealed class BookingBusinessRuleException(string message) : Exception(message);

public sealed class BookingStatusConflictException(string message) : Exception(message);

public sealed class BookingPaymentFailedException(string message) : Exception(message);
