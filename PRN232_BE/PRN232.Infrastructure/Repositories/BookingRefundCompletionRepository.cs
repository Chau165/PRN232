using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.States;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class BookingRefundCompletionRepository(AppDbContext context) : IBookingRefundCompletionRepository
{
    public async Task<IReadOnlyList<int>> ListUnfinishedAsync(int take, CancellationToken cancellationToken) =>
        await context.PayoutAttempts.AsNoTracking().Where(item => item.Status == PayoutAttemptStatuses.Succeeded &&
            (item.BookingRefundRequest!.Status == BookingRefundRequestStatuses.Refunding || item.BookingRefundRequest.Status == BookingRefundRequestStatuses.RequiresReview) &&
            !context.BookingRefunds.Any(refund => refund.BookingRefundRequestId == item.BookingRefundRequestId))
            .OrderBy(item => item.UpdatedAtUtc).ThenBy(item => item.PayoutAttemptId).Take(take).Select(item => item.PayoutAttemptId).ToListAsync(cancellationToken);

    public async Task<bool> CompleteAsync(int attemptId, DateTime now, CancellationToken cancellationToken)
    {
        var sqlite = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (sqlite) await BookingPaymentLock.RefundWriteGate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var bookingId = await context.PayoutAttempts.Where(item => item.PayoutAttemptId == attemptId)
                .Select(item => (int?)item.BookingRefundRequest!.BookingId).SingleOrDefaultAsync(cancellationToken);
            if (bookingId is null) return false;
            await BookingPaymentLock.LockAsync(context, bookingId.Value, cancellationToken);
            var completed = await BookingRefundCompletionWriter.CompleteLockedAsync(context, attemptId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return completed;
        }
        finally { if (sqlite) BookingPaymentLock.RefundWriteGate.Release(); }
    }
}
