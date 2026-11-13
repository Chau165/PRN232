using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public sealed class BookingRefundReviewService(IBookingRefundRepository repository, TimeProvider clock) : IBookingRefundReviewService
{
    private static readonly string[] Statuses = ["PendingReview", "Approved", "Refunding", "Refunded", "Rejected", "RequiresReview"];

    public async Task<IReadOnlyList<BookingRefundReviewDto>> ListAsync(string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100 || status is not null && !Statuses.Contains(status))
            throw new BookingRefundValidationException("Invalid status or pagination (page >= 1, pageSize 1..100).");
        return (await repository.ListForReviewAsync(status, (page - 1) * pageSize, pageSize, cancellationToken)).Select(Map).ToList();
    }

    public async Task<BookingRefundReviewDto?> GetAsync(int requestId, CancellationToken cancellationToken)
    {
        var item = await repository.GetForReviewAsync(requestId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<BookingRefundReviewDto> ReviewAsync(int requestId, int adminId, ReviewBookingRefundDto input, CancellationToken cancellationToken)
    {
        if (adminId <= 0) throw new BookingRefundForbiddenException();
        var note = input?.ReviewNote?.Trim();
        if (input is null || string.IsNullOrWhiteSpace(note) || note.Length > 2000 || note.Any(char.IsControl))
            throw new BookingRefundValidationException("ReviewNote is required, up to 2000 characters without control characters.");
        if (input.Approve ? input.ApprovedAmount is null || input.ApprovedAmount <= 0 ||
            input.ApprovedAmount > 9007199254740991m || input.ApprovedAmount != decimal.Truncate(input.ApprovedAmount.Value)
            : input.ApprovedAmount is not null)
            throw new BookingRefundValidationException("Approval requires a positive whole VND amount; rejection must not specify an amount.");
        return Map(await repository.ReviewAsync(requestId, adminId, input.Approve, input.ApprovedAmount, note,
            clock.GetUtcNow().UtcDateTime, cancellationToken));
    }

    private static BookingRefundReviewDto Map(BookingRefundRequests item) => new(item.BookingRefundRequestId,
        item.BookingId, item.CustomerId, item.OriginalPaymentId, item.OriginalPayment!.Amount, item.RequestedAmount,
        item.ApprovedAmount, item.Currency, item.Status, item.Reason, item.RecipientBankBin, item.RecipientAccountNumber,
        item.RecipientAccountName, false, item.ReviewedByAdminId, item.ReviewNote, UtcTimestamp.ForResponse(item.CreatedAtUtc),
        UtcTimestamp.ForResponse(item.UpdatedAtUtc), UtcTimestamp.ForResponse(item.ReviewedAtUtc), UtcTimestamp.ForResponse(item.RefundedAtUtc));
}
