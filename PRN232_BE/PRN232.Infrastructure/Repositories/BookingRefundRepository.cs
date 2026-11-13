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
    private static readonly SemaphoreSlim SqliteWriteGate = new(1, 1);

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
