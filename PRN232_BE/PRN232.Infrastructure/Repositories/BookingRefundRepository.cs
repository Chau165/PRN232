using Microsoft.EntityFrameworkCore;
using Npgsql;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class BookingRefundRepository(AppDbContext context) : IBookingRefundRepository
{
    private static readonly SemaphoreSlim SqliteWriteGate = BookingPaymentLock.RefundWriteGate;

    public async Task<IReadOnlyList<BookingRefundRequests>?> ListInScopeAsync(int bookingId, int? customerId, int? managerId, CancellationToken cancellationToken)
    {
        // Scope remains in the final query too, so reassignment cannot leak a manager's old property history.
        var bookings = context.Bookings.Where(item => item.BookingId == bookingId &&
            (customerId == null || item.CustomerId == customerId) &&
            (managerId == null || item.Room!.Property!.ManagerAssignments.Any(assignment => assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive && assignment.Manager.Role!.RoleName == RoleNames.Manager)));
        if (!await bookings.AnyAsync(cancellationToken)) return null;
        return await context.BookingRefundRequests.AsNoTracking().Where(item => bookings.Any(booking => booking.BookingId == item.BookingId))
            .OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.BookingRefundRequestId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BookingRefundRequests>> ListForReviewAsync(string? status, int skip, int take, CancellationToken cancellationToken) =>
        await context.BookingRefundRequests.AsNoTracking().Include(item => item.OriginalPayment)
            .Where(item => status == null || item.Status == status).OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.BookingRefundRequestId).Skip(skip).Take(take).ToListAsync(cancellationToken);

    public Task<BookingRefundRequests?> GetForReviewAsync(int requestId, CancellationToken cancellationToken) =>
        context.BookingRefundRequests.AsNoTracking().Include(item => item.OriginalPayment)
            .SingleOrDefaultAsync(item => item.BookingRefundRequestId == requestId, cancellationToken);

    public async Task<BookingRefundRequests> ReviewAsync(int requestId, int adminId, bool approve, decimal? amount,
        string note, DateTime now, CancellationToken cancellationToken)
    {
        var sqlite = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (sqlite) await SqliteWriteGate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            if (!await context.Users.AnyAsync(item => item.UserId == adminId && item.IsActive &&
                item.Role!.RoleName == RoleNames.Admin, cancellationToken)) throw new BookingRefundForbiddenException();
            var bookingId = await context.BookingRefundRequests.Where(item => item.BookingRefundRequestId == requestId)
                .Select(item => (int?)item.BookingId).SingleOrDefaultAsync(cancellationToken);
            if (bookingId is null) throw new BookingRefundNotFoundException();
            await BookingPaymentLock.LockAsync(context, bookingId.Value, cancellationToken);
            var request = (await GetForReviewAsync(requestId, cancellationToken))!;
            if (request.Status != BookingRefundRequestStatuses.PendingReview ||
                await context.PayoutAttempts.AnyAsync(item => item.BookingRefundRequestId == requestId, cancellationToken))
                throw new BookingRefundConflictException("Only a pending request without payout attempts can be reviewed.");
            if (approve)
            {
                var payment = request.OriginalPayment!;
                var booking = await context.Bookings.AsNoTracking().Where(item => item.BookingId == bookingId)
                    .Select(item => new { item.Status, item.CustomerId, item.DepositAmount, HasContract = item.Contract != null })
                    .SingleAsync(cancellationToken);
                if (booking.Status != BookingStatuses.Deposited || booking.HasContract || booking.CustomerId != request.CustomerId ||
                    request.Currency != "VND" || amount is null || amount <= 0 || amount > request.RequestedAmount ||
                    request.RequestedAmount != payment.Amount || payment.Amount != booking.DepositAmount ||
                    payment.Status != PaymentStatuses.Success || payment.PaymentMethod != PaymentMethods.PayOS ||
                    payment.UserId != request.CustomerId || payment.BookingId != bookingId || payment.InvoiceId != null ||
                    payment.ContractId != null || payment.ReadingId != null || string.IsNullOrWhiteSpace(request.RecipientAccountName))
                    throw new BookingRefundConflictException("Booking, deposit, refund amount or recipient name is no longer eligible.");
                if (await context.Payments.CountAsync(item => item.BookingId == bookingId && item.Status == PaymentStatuses.Success, cancellationToken) != 1 ||
                    !await context.PaymentRequests.AnyAsync(item => item.BookingId == bookingId && item.InvoiceId == null &&
                        item.CustomerId == request.CustomerId && item.PaymentId == payment.PaymentId && item.Provider == "PayOS" &&
                        item.Status == PaymentRequestStatuses.Succeeded && item.Currency == "VND" && item.Amount == payment.Amount, cancellationToken) ||
                    await context.PaymentRequests.AnyAsync(item => item.BookingId == bookingId &&
                        (item.Status == PaymentRequestStatuses.Creating || item.Status == PaymentRequestStatuses.Pending ||
                         item.Status == PaymentRequestStatuses.RequiresReview), cancellationToken) ||
                    await context.BookingRefundRequests.AnyAsync(item => item.BookingRefundRequestId != requestId &&
                        item.OriginalPaymentId == payment.PaymentId && (item.Status != BookingRefundRequestStatuses.Rejected ||
                            item.PayoutAttempts.Any(payout => payout.Status == "Succeeded" || payout.Status == "Creating" ||
                                payout.Status == "Processing" || payout.Status == "RequiresReview")), cancellationToken))
                    throw new BookingRefundConflictException("Deposit evidence is ambiguous, already claimed or requires reconciliation.");
            }
            if (now < request.UpdatedAtUtc) now = request.UpdatedAtUtc;
            // Compare-and-set remains necessary even when another writer does not use booking locks.
            var changed = await context.BookingRefundRequests.Where(item => item.BookingRefundRequestId == requestId &&
                item.Status == BookingRefundRequestStatuses.PendingReview).ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, approve ? BookingRefundRequestStatuses.Approved : BookingRefundRequestStatuses.Rejected)
                    .SetProperty(item => item.ApprovedAmount, amount).SetProperty(item => item.ReviewNote, note)
                    .SetProperty(item => item.ReviewedByAdminId, adminId).SetProperty(item => item.ReviewedAtUtc, now)
                    .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
            if (changed != 1) throw new BookingRefundConflictException("This request has already been reviewed.");
            context.Notifications.Add(new Notifications { UserId = request.CustomerId, Type = NotificationTypes.Booking,
                Title = approve ? $"Yêu cầu hoàn cọc booking #{bookingId} đã được duyệt, chờ chuyển tiền" :
                    $"Yêu cầu hoàn cọc booking #{bookingId} đã bị từ chối", IsRead = false });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (await GetForReviewAsync(requestId, cancellationToken))!;
        }
        finally { if (sqlite) SqliteWriteGate.Release(); }
    }

    public Task<BookingRefundRequests?> GetAsync(int bookingId, int requestId, int customerId, CancellationToken cancellationToken) =>
        context.BookingRefundRequests.AsNoTracking().SingleOrDefaultAsync(item => item.BookingRefundRequestId == requestId &&
            item.BookingId == bookingId && item.CustomerId == customerId, cancellationToken);

    public async Task<BookingRefundRequests> CreateAsync(BookingRefundRequests request, CancellationToken cancellationToken)
    {
        var sqlite = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (sqlite) await SqliteWriteGate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await BookingPaymentLock.LockAsync(context, request.BookingId, cancellationToken);
            var booking = await context.Bookings.AsNoTracking().Where(item => item.BookingId == request.BookingId)
                .Select(item => new { item.CustomerId, item.Status, item.DepositAmount, HasContract = item.Contract != null })
                .SingleOrDefaultAsync(cancellationToken);
            if (booking is null) throw new BookingRefundNotFoundException();
            if (booking.CustomerId != request.CustomerId) throw new BookingRefundForbiddenException();
            var refundStatuses = await context.BookingRefundRequests.Where(item => item.BookingId == request.BookingId)
                .Select(item => item.Status).ToListAsync(cancellationToken);
            var payments = await context.Payments.AsNoTracking().Where(item => item.BookingId == request.BookingId &&
                item.Status == PaymentStatuses.Success).ToListAsync(cancellationToken);
            if (!BookingRefundPolicy.CanRequestRefund(booking.Status, booking.HasContract, payments.Count == 1,
                refundStatuses.Any(status => status != BookingRefundRequestStatuses.Rejected) ? "Claimed" : null))
                throw new BookingRefundConflictException("Booking must be deposited, without a contract or an existing refund claim.");
            var payment = payments.Single();
            if (payment.UserId != request.CustomerId || payment.PaymentMethod != PaymentMethods.PayOS ||
                payment.InvoiceId.HasValue || payment.ContractId.HasValue || payment.ReadingId.HasValue ||
                payment.Amount != booking.DepositAmount || payment.Amount <= 0 || payment.Amount > 9007199254740991m ||
                payment.Amount != decimal.Truncate(payment.Amount))
                throw new BookingRefundConflictException("A single verified payOS booking deposit is required; ambiguous or legacy payments need review.");
            var verified = await context.PaymentRequests.AnyAsync(item => item.BookingId == request.BookingId &&
                item.InvoiceId == null && item.CustomerId == request.CustomerId && item.PaymentId == payment.PaymentId &&
                item.Provider == "PayOS" && item.Status == PaymentRequestStatuses.Succeeded && item.Currency == "VND" &&
                item.Amount == payment.Amount, cancellationToken);
            var unresolved = await context.PaymentRequests.AnyAsync(item => item.BookingId == request.BookingId &&
                (item.Status == PaymentRequestStatuses.Creating || item.Status == PaymentRequestStatuses.Pending ||
                 item.Status == PaymentRequestStatuses.RequiresReview), cancellationToken);
            if (!verified || unresolved || await context.BookingRefundRequests.AnyAsync(item => item.OriginalPaymentId == payment.PaymentId &&
                item.Status != BookingRefundRequestStatuses.Rejected, cancellationToken))
                throw new BookingRefundConflictException("Deposit evidence is missing, already claimed, or requires reconciliation.");
            request.OriginalPaymentId = payment.PaymentId;
            request.RequestedAmount = payment.Amount;
            context.BookingRefundRequests.Add(request);
            var admins = await context.Users.Where(item => item.IsActive && item.Role!.RoleName == RoleNames.Admin)
                .Select(item => item.UserId).ToListAsync(cancellationToken);
            foreach (var admin in admins) context.Notifications.Add(new Notifications { UserId = admin,
                Type = NotificationTypes.Booking, Title = $"Yêu cầu hoàn cọc booking #{request.BookingId} đang chờ duyệt", IsRead = false });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return request;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505", ConstraintName:
            "UX_BookingRefundRequests_BookingId_Claimed" or "UX_BookingRefundRequests_OriginalPaymentId_Claimed" })
        { throw new BookingRefundConflictException("A refund claim already exists. Reload the booking."); }
        finally { if (sqlite) SqliteWriteGate.Release(); }
    }
}
