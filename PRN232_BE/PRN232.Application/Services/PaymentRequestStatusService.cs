using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.States;

namespace PRN232.Application.Services;

public class PaymentRequestStatusService(IPaymentRequestStatusRepository repository, TimeProvider timeProvider) : IPaymentRequestStatusService
{
    public async Task<PaymentRequestStatusDto?> GetByIdAsync(int id, int userId, string role, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new PaymentRequestStatusValidationException();
        var scope = AccessScope.For(userId, role);
        if (userId <= 0 || scope is null) throw new PaymentRequestStatusForbiddenException();
        var request = await repository.GetByIdAsync(id, scope.Value.CustomerId, scope.Value.ManagerId, cancellationToken);
        if (request is null)
        {
            if (await repository.ExistsAsync(id, cancellationToken)) throw new PaymentRequestStatusForbiddenException();
            return null;
        }
        var expired = request.Status == PaymentRequestStatuses.Expired ||
            ((request.Status == PaymentRequestStatuses.Creating || request.Status == PaymentRequestStatuses.Pending) &&
                request.ExpiresAtUtc.HasValue && request.ExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime);
        var canOpen = request.Status == PaymentRequestStatuses.Pending && request.ExpiresAtUtc.HasValue && !expired &&
            ((request.InvoiceId.HasValue && request.InvoiceStatus is InvoiceStatuses.Unpaid or InvoiceStatuses.Overdue) ||
             (request.BookingId.HasValue && request.BookingStatus == BookingStatuses.Pending && !request.BookingHasContract)) && !string.IsNullOrWhiteSpace(request.CheckoutUrl);
        return new PaymentRequestStatusDto(request.PaymentRequestId, request.InvoiceId, request.CustomerId, request.PropertyId,
            request.Status, request.InvoiceStatus, request.Amount, request.Currency, UtcTimestamp.ForResponse(request.CreatedAtUtc), UtcTimestamp.ForResponse(request.UpdatedAtUtc),
            UtcTimestamp.ForResponse(request.ExpiresAtUtc), UtcTimestamp.ForResponse(request.ConfirmedAtUtc), request.PaymentId, UtcTimestamp.ForResponse(request.PaidAtUtc),
            expired, canOpen, canOpen ? request.CheckoutUrl : null, request.BookingId, request.BookingStatus);
    }
}

public sealed class PaymentRequestStatusForbiddenException() : Exception("You cannot access this payment request.");
public sealed class PaymentRequestStatusValidationException() : Exception("Payment request ID must be positive.");
