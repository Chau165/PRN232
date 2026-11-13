using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class PayoutReconciliationRepository(AppDbContext context) : IPayoutReconciliationRepository
{
    private IQueryable<PayoutAttempts> Due(PayoutReconciliationWindow window) => context.PayoutAttempts.Where(item =>
        (item.BookingRefundRequest!.Status == BookingRefundRequestStatuses.Refunding || item.BookingRefundRequest.Status == BookingRefundRequestStatuses.RequiresReview) &&
        item.CreatedAtUtc <= window.StartedBeforeUtc && (item.LastCheckedAtUtc == null || item.LastCheckedAtUtc <= window.DueBeforeUtc) &&
        !context.PayoutAttempts.Any(newer => newer.BookingRefundRequestId == item.BookingRefundRequestId && newer.AttemptNumber > item.AttemptNumber) &&
        (item.Status == PayoutAttemptStatuses.Creating || item.Status == PayoutAttemptStatuses.Processing || item.Status == PayoutAttemptStatuses.RequiresReview ||
         window.RetryFailedPayouts && item.AttemptNumber < window.MaxPayoutAttempts && item.CompletedAtUtc <= window.RetryBeforeUtc &&
            (item.Status == PayoutAttemptStatuses.CreateFailed || item.Status == PayoutAttemptStatuses.TransferFailed)));

    public async Task<IReadOnlyList<int>> ListDueAsync(PayoutReconciliationWindow window, int take, CancellationToken cancellationToken) =>
        await Due(window).AsNoTracking().OrderBy(item => item.LastCheckedAtUtc ?? item.CreatedAtUtc).ThenBy(item => item.PayoutAttemptId)
            .Take(take).Select(item => item.PayoutAttemptId).ToListAsync(cancellationToken);

    public async Task<PayoutAttempts?> TryLeaseAsync(int attemptId, PayoutReconciliationWindow window, CancellationToken cancellationToken)
    {
        var sqlite = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (sqlite) await BookingPaymentLock.RefundWriteGate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var bookingId = await context.PayoutAttempts.Where(item => item.PayoutAttemptId == attemptId)
                .Select(item => (int?)item.BookingRefundRequest!.BookingId).SingleOrDefaultAsync(cancellationToken);
            if (bookingId is null) return null;
            await BookingPaymentLock.LockAsync(context, bookingId.Value, cancellationToken);
            var lease = await Due(window).AsNoTracking().SingleOrDefaultAsync(item => item.PayoutAttemptId == attemptId, cancellationToken);
            if (lease is null) return null;
            // PostgreSQL persists timestamps at microsecond precision. The lease token must round-trip exactly.
            var checkedAt = new DateTime(window.NowUtc.Ticks - window.NowUtc.Ticks % 10, DateTimeKind.Utc);
            var updated = window.NowUtc > lease.UpdatedAtUtc ? window.NowUtc : lease.UpdatedAtUtc;
            var changed = await Due(window).Where(item => item.PayoutAttemptId == attemptId).ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LastCheckedAtUtc, checkedAt).SetProperty(item => item.UpdatedAtUtc, updated), cancellationToken);
            if (changed != 1) return null;
            await transaction.CommitAsync(cancellationToken);
            lease.LastCheckedAtUtc = checkedAt; lease.UpdatedAtUtc = updated;
            return lease;
        }
        finally { if (sqlite) BookingPaymentLock.RefundWriteGate.Release(); }
    }

    public async Task ApplyAsync(PayoutAttempts lease, PayoutReconciliationResult result, PayoutReconciliationWindow window,
        DateTime now, CancellationToken cancellationToken)
    {
        var sqlite = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (sqlite) await BookingPaymentLock.RefundWriteGate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var bookingId = await context.PayoutAttempts.Where(item => item.PayoutAttemptId == lease.PayoutAttemptId)
                .Select(item => (int?)item.BookingRefundRequest!.BookingId).SingleOrDefaultAsync(cancellationToken);
            if (bookingId is null) return;
            await BookingPaymentLock.LockAsync(context, bookingId.Value, cancellationToken);
            var current = await context.PayoutAttempts.AsNoTracking().Include(item => item.BookingRefundRequest)
                .SingleAsync(item => item.PayoutAttemptId == lease.PayoutAttemptId, cancellationToken);
            var refund = current.BookingRefundRequest!;
            if (current.Status != lease.Status || current.LastCheckedAtUtc != lease.LastCheckedAtUtc ||
                refund.Status is not (BookingRefundRequestStatuses.Refunding or BookingRefundRequestStatuses.RequiresReview) ||
                await context.PayoutAttempts.AnyAsync(item => item.BookingRefundRequestId == current.BookingRefundRequestId &&
                    item.AttemptNumber > current.AttemptNumber, cancellationToken)) return;
            now = new[] { now, current.UpdatedAtUtc, refund.UpdatedAtUtc }.Max();

            if (result.Outcome == PayoutReconciliationOutcome.Retry)
            {
                // Terminal attempts stay immutable. Only P8 can create a new attempt after this verified recovery.
                if (!window.RetryFailedPayouts || current.AttemptNumber >= window.MaxPayoutAttempts ||
                    current.CompletedAtUtc > window.RetryBeforeUtc || !PayoutReconciliationPolicy.HasDefinitiveFailure(current) ||
                    refund.Status != BookingRefundRequestStatuses.RequiresReview || refund.ApprovedAmount != current.Amount ||
                    refund.Currency != current.Currency || refund.RecipientBankBin != current.RecipientBankBin ||
                    refund.RecipientAccountNumber != current.RecipientAccountNumber || refund.RecipientAccountName != current.RecipientAccountName ||
                    !await context.Bookings.AnyAsync(item => item.BookingId == refund.BookingId && item.Status == BookingStatuses.Deposited && item.Contract == null, cancellationToken) ||
                    await context.PayoutAttempts.AnyAsync(item => item.BookingRefundRequestId == refund.BookingRefundRequestId &&
                        item.Status != PayoutAttemptStatuses.CreateFailed && item.Status != PayoutAttemptStatuses.TransferFailed, cancellationToken)) return;
                await context.BookingRefundRequests.Where(item => item.BookingRefundRequestId == refund.BookingRefundRequestId &&
                    item.Status == BookingRefundRequestStatuses.RequiresReview).ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, BookingRefundRequestStatuses.Approved).SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            if (!PayoutAttemptStatuses.IsActive(current.Status))
            {
                if (current.Status == PayoutAttemptStatuses.TransferFailed && result.ErrorCode == "PAYOUT_FAILURE_RECHECK_REQUIRED")
                {
                    await context.PayoutAttempts.Where(item => item.PayoutAttemptId == current.PayoutAttemptId &&
                        item.Status == lease.Status && item.LastCheckedAtUtc == lease.LastCheckedAtUtc).ExecuteUpdateAsync(setters => setters
                            .SetProperty(item => item.LastErrorCode, "PAYOUT_FAILURE_RECHECK_REQUIRED").SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
                    await NotifyAdminsAsync(current.PayoutAttemptId, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                return;
            }
            var remote = result.Remote;
            var succeeded = result.Outcome == PayoutReconciliationOutcome.Succeeded;
            var failed = result.Outcome == PayoutReconciliationOutcome.Failed;
            var processing = result.Outcome == PayoutReconciliationOutcome.Processing;
            if ((succeeded || failed || processing) && remote is null) throw new InvalidOperationException("Payout reconciliation requires provider evidence.");
            var status = succeeded ? PayoutAttemptStatuses.Succeeded : failed ? PayoutAttemptStatuses.TransferFailed :
                processing ? PayoutAttemptStatuses.Processing : PayoutAttemptStatuses.RequiresReview;
            var refundStatus = succeeded || processing ? BookingRefundRequestStatuses.Refunding : BookingRefundRequestStatuses.RequiresReview;
            var submitted = current.SubmittedAtUtc ?? (remote is not null ? current.CreatedAtUtc : (DateTime?)null);
            var bankReference = string.IsNullOrWhiteSpace(remote?.BankReference) ? current.BankReference : remote.BankReference;
            var changed = await context.PayoutAttempts.Where(item => item.PayoutAttemptId == current.PayoutAttemptId &&
                item.Status == lease.Status && item.LastCheckedAtUtc == lease.LastCheckedAtUtc).ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, status).SetProperty(item => item.UpdatedAtUtc, now)
                    .SetProperty(item => item.ProviderPayoutId, remote != null ? remote.ProviderPayoutId : current.ProviderPayoutId)
                    .SetProperty(item => item.ProviderTransactionId, remote != null ? remote.ProviderTransactionId : current.ProviderTransactionId)
                    .SetProperty(item => item.ProviderApprovalState, remote != null ? remote.ApprovalState : current.ProviderApprovalState)
                    .SetProperty(item => item.ProviderTransactionState, remote != null ? remote.TransactionState : current.ProviderTransactionState)
                    .SetProperty(item => item.SubmittedAtUtc, submitted).SetProperty(item => item.BankReference, bankReference)
                    .SetProperty(item => item.TransferredAtUtc, succeeded ? result.TransferredAtUtc : null)
                    .SetProperty(item => item.CompletedAtUtc, succeeded || failed ? (DateTime?)now : null)
                    .SetProperty(item => item.LastErrorCode, result.ErrorCode).SetProperty(item => item.LastErrorMessage, (string?)null), cancellationToken);
            if (changed == 1)
            {
                await context.BookingRefundRequests.Where(item => item.BookingRefundRequestId == refund.BookingRefundRequestId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, refundStatus).SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
                if (refundStatus == BookingRefundRequestStatuses.RequiresReview) await NotifyAdminsAsync(current.PayoutAttemptId, cancellationToken);
                if (succeeded) await BookingRefundCompletionWriter.CompleteLockedAsync(context, current.PayoutAttemptId, now, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        finally { if (sqlite) BookingPaymentLock.RefundWriteGate.Release(); }
    }

    private async Task NotifyAdminsAsync(int attemptId, CancellationToken cancellationToken)
    {
        var title = $"Lệnh hoàn cọc #{attemptId} cần kiểm tra";
        var admins = await context.Users.Where(item => item.IsActive && item.Role!.RoleName == RoleNames.Admin &&
            !context.Notifications.Any(note => note.UserId == item.UserId && note.Type == NotificationTypes.Booking && note.Title == title))
            .Select(item => item.UserId).ToListAsync(cancellationToken);
        foreach (var admin in admins) context.Notifications.Add(new Notifications { UserId = admin, Type = NotificationTypes.Booking, Title = title, IsRead = false });
        await context.SaveChangesAsync(cancellationToken);
    }
}
