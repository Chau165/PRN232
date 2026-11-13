using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public sealed class BookingRefundService(IBookingRefundRepository repository, TimeProvider clock) : IBookingRefundService
{
    public async Task<BookingRefundDto> CreateAsync(int bookingId, int customerId, CreateBookingRefundDto input, CancellationToken cancellationToken)
    {
        if (customerId <= 0) throw new BookingRefundForbiddenException();
        if (bookingId <= 0 || input is null) throw new BookingRefundValidationException("A valid booking and request are required.");
        var reason = input.Reason?.Trim();
        var name = input.RecipientAccountName?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000 || reason.Any(char.IsControl))
            throw new BookingRefundValidationException("Reason is required, up to 2000 characters without control characters.");
        if (name is not null && (name.Length == 0 || name.Length > 256 || name.Any(char.IsControl)))
            throw new BookingRefundValidationException("Recipient account name must be 1..256 characters without control characters.");
        var bin = input.RecipientBankBin?.Trim() ?? "";
        var account = input.RecipientAccountNumber?.Trim() ?? "";
        try { PayoutPreflightValidation.Validate(new PayoutPreflightRequest("refund", 1, bin, account, "Hoan coc")); }
        catch (PayOSPayoutValidationException exception) { throw new BookingRefundValidationException(exception.Message); }
        var now = clock.GetUtcNow().UtcDateTime;
        var saved = await repository.CreateAsync(new BookingRefundRequests
        {
            BookingId = bookingId, CustomerId = customerId, Reason = reason,
            RecipientBankBin = bin, RecipientAccountNumber = account, RecipientAccountName = name,
            Status = BookingRefundRequestStatuses.PendingReview, Currency = "VND", CreatedAtUtc = now, UpdatedAtUtc = now
        }, cancellationToken);
        return ConvertToDto(saved);
    }

    public async Task<BookingRefundDto?> GetAsync(int bookingId, int requestId, int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) throw new BookingRefundForbiddenException();
        var request = await repository.GetAsync(bookingId, requestId, customerId, cancellationToken);
        return request is null ? null : ConvertToDto(request);
    }

    private static BookingRefundDto ConvertToDto(BookingRefundRequests item) => new(item.BookingRefundRequestId,
        item.BookingId, item.OriginalPaymentId, item.RequestedAmount, item.Currency, item.Status, item.Reason,
        item.RecipientBankBin, "****" + item.RecipientAccountNumber[^Math.Min(4, item.RecipientAccountNumber.Length)..],
        item.RecipientAccountName, false, UtcTimestamp.ForResponse(item.CreatedAtUtc), UtcTimestamp.ForResponse(item.UpdatedAtUtc));
}
