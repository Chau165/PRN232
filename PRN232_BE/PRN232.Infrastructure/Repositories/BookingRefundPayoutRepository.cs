using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class BookingRefundPayoutRepository(AppDbContext context) : IBookingRefundPayoutRepository
{
    public async Task<IReadOnlyList<int>> ListApprovedAsync(int take, CancellationToken cancellationToken) =>
        await context.BookingRefundRequests.AsNoTracking().Where(item => item.Status == BookingRefundRequestStatuses.Approved &&
            item.PayoutAttempts.All(attempt => attempt.Status == PayoutAttemptStatuses.CreateFailed ||
                attempt.Status == PayoutAttemptStatuses.TransferFailed)).OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.BookingRefundRequestId)
            .Take(take).Select(item => item.BookingRefundRequestId).ToListAsync(cancellationToken);

    public async Task<PayoutAttempts?> TryClaimAsync(int refundRequestId, DateTime now, CancellationToken cancellationToken)
    {
        var sqlite = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (sqlite) await BookingPaymentLock.RefundWriteGate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var bookingId = await context.BookingRefundRequests.Where(item => item.BookingRefundRequestId == refundRequestId)
                .Select(item => (int?)item.BookingId).SingleOrDefaultAsync(cancellationToken);
            if (bookingId is null) return null;
            await BookingPaymentLock.LockAsync(context, bookingId.Value, cancellationToken);
            var refund = await context.BookingRefundRequests.AsNoTracking().Include(item => item.Booking).ThenInclude(item => item!.Contract)
                .Include(item => item.OriginalPayment).SingleAsync(item => item.BookingRefundRequestId == refundRequestId, cancellationToken);
            var history = await context.PayoutAttempts.AsNoTracking().Where(item => item.BookingRefundRequestId == refundRequestId)
                .OrderByDescending(item => item.AttemptNumber).ToListAsync(cancellationToken);
            if (refund.Status != BookingRefundRequestStatuses.Approved || history.Any(item =>
                !PayoutAttemptStatuses.CanRetryAsNewAttempt(item.Status))) return null;
            if (now < refund.UpdatedAtUtc) now = refund.UpdatedAtUtc;
            if (!await IsEligibleAsync(refund, cancellationToken))
            {
                await context.BookingRefundRequests.Where(item => item.BookingRefundRequestId == refundRequestId &&
                    item.Status == BookingRefundRequestStatuses.Approved).ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, BookingRefundRequestStatuses.RequiresReview)
                        .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }
            var key = Guid.NewGuid().ToString("N");
            var attempt = new PayoutAttempts
            {
                BookingRefundRequestId = refundRequestId, AttemptNumber = (history.FirstOrDefault()?.AttemptNumber ?? 0) + 1,
                InitiatedByAdminId = refund.ReviewedByAdminId!.Value,
                ReferenceId = $"refund_{refundRequestId}_{key}", IdempotencyKey = key, Amount = refund.ApprovedAmount!.Value,
                Currency = refund.Currency, RecipientBankBin = refund.RecipientBankBin, RecipientAccountNumber = refund.RecipientAccountNumber,
                RecipientAccountName = refund.RecipientAccountName!, Description = $"Hoan coc booking {refund.BookingId}",
                Status = PayoutAttemptStatuses.Creating, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var changed = await context.BookingRefundRequests.Where(item => item.BookingRefundRequestId == refundRequestId &&
                item.Status == BookingRefundRequestStatuses.Approved).ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, BookingRefundRequestStatuses.Refunding).SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
            if (changed != 1) return null;
            context.PayoutAttempts.Add(attempt);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return attempt;
        }
        finally { if (sqlite) BookingPaymentLock.RefundWriteGate.Release(); }
    }

    private async Task<bool> IsEligibleAsync(BookingRefundRequests refund, CancellationToken cancellationToken)
    {
        var booking = refund.Booking!;
        var payment = refund.OriginalPayment!;
        if (!BookingRefundPolicy.CanStartPayout(booking.Status, booking.Contract != null, refund.Status, null) ||
            refund.CustomerId != booking.CustomerId || refund.Currency != "VND" || refund.ApprovedAmount is not > 0 ||
            refund.ApprovedAmount > refund.RequestedAmount || refund.RequestedAmount != payment.Amount ||
            payment.Amount != booking.DepositAmount || payment.BookingId != refund.BookingId || payment.UserId != refund.CustomerId ||
            payment.Status != PaymentStatuses.Success || payment.PaymentMethod != PaymentMethods.PayOS ||
            payment.InvoiceId != null || payment.ContractId != null || payment.ReadingId != null ||
            string.IsNullOrWhiteSpace(refund.RecipientAccountName) || refund.ReviewedByAdminId is null) return false;
        try { PayoutPreflightValidation.Validate(new("refund", refund.ApprovedAmount.Value,
            refund.RecipientBankBin, refund.RecipientAccountNumber, "Hoan coc")); }
        catch (PayOSPayoutValidationException) { return false; }
        return await context.Users.AnyAsync(item => item.UserId == refund.ReviewedByAdminId && item.IsActive && item.Role!.RoleName == RoleNames.Admin, cancellationToken) &&
            await context.Payments.CountAsync(item => item.BookingId == refund.BookingId && item.Status == PaymentStatuses.Success, cancellationToken) == 1 &&
            await context.PaymentRequests.AnyAsync(item => item.BookingId == refund.BookingId && item.InvoiceId == null && item.CustomerId == refund.CustomerId &&
                item.PaymentId == payment.PaymentId && item.Provider == "PayOS" && item.Status == PaymentRequestStatuses.Succeeded &&
                item.Currency == "VND" && item.Amount == payment.Amount, cancellationToken) &&
            !await context.PaymentRequests.AnyAsync(item => item.BookingId == refund.BookingId &&
                (item.Status == PaymentRequestStatuses.Creating || item.Status == PaymentRequestStatuses.Pending || item.Status == PaymentRequestStatuses.RequiresReview), cancellationToken) &&
            !await context.BookingRefundRequests.AnyAsync(item => item.BookingRefundRequestId != refund.BookingRefundRequestId &&
                item.OriginalPaymentId == payment.PaymentId && (item.Status != BookingRefundRequestStatuses.Rejected ||
                    item.PayoutAttempts.Any(attempt => attempt.Status != PayoutAttemptStatuses.CreateFailed &&
                        attempt.Status != PayoutAttemptStatuses.TransferFailed)), cancellationToken);
    }

    public async Task RecordOutcomeAsync(int attemptId, PayoutDispatchOutcome outcome, PayoutSubmissionReceipt? receipt,
        string? errorCode, DateTime now, DateTime? submittedAt, CancellationToken cancellationToken)
    {
        var sqlite = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (sqlite) await BookingPaymentLock.RefundWriteGate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var attempt = await context.PayoutAttempts.AsNoTracking().Include(item => item.BookingRefundRequest)
                .SingleAsync(item => item.PayoutAttemptId == attemptId, cancellationToken);
            await BookingPaymentLock.LockAsync(context, attempt.BookingRefundRequest!.BookingId, cancellationToken);
            var accepted = outcome == PayoutDispatchOutcome.Accepted;
            if (accepted && (receipt is null || submittedAt is null)) throw new InvalidOperationException("Accepted payout requires a receipt.");
            now = new[] { now, attempt.UpdatedAtUtc, attempt.BookingRefundRequest.UpdatedAtUtc, submittedAt ?? attempt.CreatedAtUtc }.Max();
            var changed = await context.PayoutAttempts.Where(item => item.PayoutAttemptId == attemptId && item.Status == PayoutAttemptStatuses.Creating &&
                item.BookingRefundRequest!.Status == BookingRefundRequestStatuses.Refunding).ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, accepted ? PayoutAttemptStatuses.Processing :
                        outcome == PayoutDispatchOutcome.NotSent ? PayoutAttemptStatuses.CreateFailed : PayoutAttemptStatuses.RequiresReview)
                    .SetProperty(item => item.ProviderPayoutId, accepted ? receipt!.ProviderPayoutId : null)
                    .SetProperty(item => item.ProviderTransactionId, accepted ? receipt!.ProviderTransactionId : null)
                    .SetProperty(item => item.ProviderApprovalState, accepted ? receipt!.ApprovalState : null)
                    .SetProperty(item => item.ProviderTransactionState, accepted ? receipt!.TransactionState : null)
                    .SetProperty(item => item.SubmittedAtUtc, submittedAt).SetProperty(item => item.UpdatedAtUtc, now)
                    .SetProperty(item => item.CompletedAtUtc, outcome == PayoutDispatchOutcome.NotSent ? (DateTime?)now : null)
                    .SetProperty(item => item.LastErrorCode, accepted ? null : errorCode ?? "PAYOUT_SUBMISSION_UNCONFIRMED"), cancellationToken);
            if (changed == 1)
                await context.BookingRefundRequests.Where(item => item.BookingRefundRequestId == attempt.BookingRefundRequestId &&
                    item.Status == BookingRefundRequestStatuses.Refunding).ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, accepted ? BookingRefundRequestStatuses.Refunding : BookingRefundRequestStatuses.RequiresReview)
                        .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally { if (sqlite) BookingPaymentLock.RefundWriteGate.Release(); }
    }
}
