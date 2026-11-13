using Microsoft.EntityFrameworkCore;
using PRN232.Application.Policies;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

internal static class BookingRefundCompletionWriter
{
    // Caller owns the transaction and the room -> booking locks. No external calls here.
    internal static async Task<bool> CompleteLockedAsync(AppDbContext context, int attemptId, DateTime now, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Refund completion requires a transaction.");
        var attempt = await context.PayoutAttempts.AsNoTracking().Include(item => item.BookingRefundRequest).ThenInclude(item => item!.Booking)
            .ThenInclude(item => item!.Contract).Include(item => item.BookingRefundRequest).ThenInclude(item => item!.OriginalPayment)
            .SingleOrDefaultAsync(item => item.PayoutAttemptId == attemptId, cancellationToken);
        if (attempt is null || attempt.Status != PayoutAttemptStatuses.Succeeded) return false;
        var request = attempt.BookingRefundRequest!;
        var booking = request.Booking!;
        var payment = request.OriginalPayment!;
        // Keep real clock time for other customers' holds, independently of monotonic audit timestamps.
        var availabilityInstant = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        var existing = await context.BookingRefunds.AsNoTracking().SingleOrDefaultAsync(item => item.BookingRefundRequestId == request.BookingRefundRequestId, cancellationToken);
        if (existing is not null) return existing.PayoutAttemptId == attemptId && request.Status == BookingRefundRequestStatuses.Refunded && booking.Status == BookingStatuses.Cancelled;
        now = DateTime.SpecifyKind(new[] { now, attempt.UpdatedAtUtc, request.UpdatedAtUtc, attempt.CompletedAtUtc ?? attempt.CreatedAtUtc }.Max(), DateTimeKind.Utc);

        var valid = BookingRefundPolicy.CanFinalizeCancellation(booking.Status, booking.Contract != null, request.Status, attempt.Status) &&
            attempt.Provider == "PayOS" && attempt.Currency == "VND" && request.Currency == "VND" &&
            attempt.ProviderApprovalState == "Completed" && attempt.ProviderTransactionState == "Succeeded" &&
            !string.IsNullOrWhiteSpace(attempt.ProviderPayoutId) && !string.IsNullOrWhiteSpace(attempt.ProviderTransactionId) &&
            attempt.CompletedAtUtc is not null && attempt.SubmittedAtUtc is not null && request.ApprovedAmount == attempt.Amount &&
            attempt.Amount > 0 && attempt.Amount <= request.RequestedAmount && request.RequestedAmount == booking.DepositAmount &&
            request.RequestedAmount == payment.Amount && request.CustomerId == booking.CustomerId && payment.UserId == request.CustomerId &&
            payment.BookingId == booking.BookingId && payment.Status == PaymentStatuses.Success && payment.PaymentMethod == PaymentMethods.PayOS &&
            payment.InvoiceId is null && payment.ContractId is null && payment.ReadingId is null &&
            attempt.RecipientBankBin == request.RecipientBankBin && attempt.RecipientAccountNumber == request.RecipientAccountNumber &&
            attempt.RecipientAccountName == request.RecipientAccountName;
        if (valid)
            valid = await context.Payments.CountAsync(item => item.BookingId == booking.BookingId && item.Status == PaymentStatuses.Success, cancellationToken) == 1 &&
                await context.PaymentRequests.AnyAsync(item => item.BookingId == booking.BookingId && item.InvoiceId == null && item.CustomerId == request.CustomerId &&
                    item.PaymentId == payment.PaymentId && item.Provider == "PayOS" && item.Status == PaymentRequestStatuses.Succeeded && item.Currency == "VND" && item.Amount == payment.Amount, cancellationToken) &&
                !await context.PayoutAttempts.AnyAsync(item => item.BookingRefundRequestId == request.BookingRefundRequestId && item.PayoutAttemptId != attemptId &&
                    (item.AttemptNumber > attempt.AttemptNumber || item.Status != PayoutAttemptStatuses.CreateFailed && item.Status != PayoutAttemptStatuses.TransferFailed), cancellationToken) &&
                !await context.BookingRefunds.AnyAsync(item => item.OriginalPaymentId == payment.PaymentId || item.BookingId == booking.BookingId, cancellationToken) &&
                !await context.BookingRefundRequests.AnyAsync(item => item.OriginalPaymentId == payment.PaymentId && item.BookingRefundRequestId != request.BookingRefundRequestId &&
                    item.PayoutAttempts.Any(payout => payout.Status == PayoutAttemptStatuses.Succeeded), cancellationToken);
        if (!valid)
        {
            // Money is already confirmed: retain Succeeded, but never release an inconsistent booking/contract.
            if (request.Status is BookingRefundRequestStatuses.Refunding or BookingRefundRequestStatuses.RequiresReview)
                await context.BookingRefundRequests.Where(item => item.BookingRefundRequestId == request.BookingRefundRequestId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, BookingRefundRequestStatuses.RequiresReview)
                        .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
            await context.PayoutAttempts.Where(item => item.PayoutAttemptId == attemptId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
            var title = $"Hoàn cọc #{request.BookingRefundRequestId} chưa thể hoàn tất";
            var admins = await context.Users.Where(item => item.IsActive && item.Role!.RoleName == RoleNames.Admin &&
                !context.Notifications.Any(note => note.UserId == item.UserId && note.Type == NotificationTypes.Booking && note.Title == title))
                .Select(item => item.UserId).ToListAsync(cancellationToken);
            foreach (var admin in admins) context.Notifications.Add(new Notifications { UserId = admin, Type = NotificationTypes.Booking, Title = title, IsRead = false });
            await context.SaveChangesAsync(cancellationToken);
            return false;
        }

        var changed = await context.Bookings.Where(item => item.BookingId == booking.BookingId && item.Status == BookingStatuses.Deposited && item.Contract == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, BookingStatuses.Cancelled), cancellationToken);
        if (changed != 1) throw new InvalidOperationException("Booking changed during refund completion.");
        // Available rooms are held by the booking policy, not a persisted Reserved status.
        // A legacy Reserved room may open only if the common rentability policy sees no other hold/contract.
        var released = await context.Rooms.Where(item => item.RoomId == booking.RoomId && item.Status == RoomStatuses.Reserved)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, RoomStatuses.Available), cancellationToken);
        if (released == 1 && !await context.Rooms.Where(RoomRentabilityPolicy.IsRentableAt(availabilityInstant))
            .AnyAsync(item => item.RoomId == booking.RoomId, cancellationToken))
            await context.Rooms.Where(item => item.RoomId == booking.RoomId && item.Status == RoomStatuses.Available)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, RoomStatuses.Reserved), cancellationToken);

        await context.BookingRefundRequests.Where(item => item.BookingRefundRequestId == request.BookingRefundRequestId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, BookingRefundRequestStatuses.Refunded)
                .SetProperty(item => item.RefundedAtUtc, now).SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
        context.BookingRefunds.Add(new BookingRefunds { BookingRefundRequestId = request.BookingRefundRequestId, PayoutAttemptId = attemptId,
            OriginalPaymentId = payment.PaymentId, BookingId = booking.BookingId, CustomerId = request.CustomerId,
            Amount = attempt.Amount, Currency = attempt.Currency, RecordedAtUtc = now, TransferredAtUtc = attempt.TransferredAtUtc });

        var propertyId = await context.Rooms.Where(item => item.RoomId == booking.RoomId).Select(item => item.PropertyId).SingleAsync(cancellationToken);
        if (context.Database.IsNpgsql())
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"PropertyManagerAssignments\" WHERE \"PropertyId\" = {propertyId} FOR SHARE", cancellationToken);
        var recipients = await context.Users.Where(item => item.IsActive && item.Role!.RoleName == RoleNames.Admin).Select(item => item.UserId).ToListAsync(cancellationToken);
        recipients.AddRange(await context.PropertyManagerAssignments.Where(item => item.PropertyId == propertyId && item.Manager!.IsActive &&
            item.Manager.Role!.RoleName == RoleNames.Manager).Select(item => item.ManagerId).ToListAsync(cancellationToken));
        recipients.Add(request.CustomerId);
        foreach (var recipient in recipients.Distinct()) context.Notifications.Add(new Notifications { UserId = recipient,
            Type = NotificationTypes.Booking, Title = $"Đã hoàn cọc và hủy booking #{booking.BookingId}", IsRead = false });
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
