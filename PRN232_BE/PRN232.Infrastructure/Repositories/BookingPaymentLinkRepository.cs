using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Application.Policies;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class BookingPaymentLinkRepository(AppDbContext context) : IBookingPaymentLinkRepository
{
    public async Task<(PaymentRequests Request, bool Created)> ReserveAsync(int bookingId, int customerId,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await BookingPaymentLock.LockAsync(context, bookingId, cancellationToken);
        var booking = await context.Bookings.AsNoTracking().Where(item => item.BookingId == bookingId)
            .Select(item => new { item.CustomerId, item.RoomId, item.Status, item.DepositAmount, item.ExpiryDate, HasContract = item.Contract != null })
            .SingleOrDefaultAsync(cancellationToken);
        if (booking is null) throw new BookingNotFoundException("Booking does not exist.");
        if (booking.CustomerId != customerId) throw new BookingAuthorizationException("You cannot pay another customer's booking.");
        if (booking.Status != BookingStatuses.Pending || booking.HasContract || booking.ExpiryDate <= nowUtc ||
            await context.Payments.AnyAsync(item => item.BookingId == bookingId && item.Status == PaymentStatuses.Success, cancellationToken) ||
            !await BookingPaymentLock.RoomCanAcceptDepositAsync(context, booking.RoomId, bookingId, nowUtc, cancellationToken))
            throw new BookingStatusConflictException("Booking expired, already paid, contracted or the room is no longer available.");
        var amount = booking.DepositAmount;
        if (amount <= 0 || amount != decimal.Truncate(amount) || amount > 9007199254740991m)
            throw new BookingBusinessRuleException("payOS requires a positive whole VND deposit within the supported integer range.");
        var active = await context.PaymentRequests.AsNoTracking().SingleOrDefaultAsync(item => item.BookingId == bookingId &&
            (item.Status == PaymentRequestStatuses.Creating || item.Status == PaymentRequestStatuses.Pending || item.Status == PaymentRequestStatuses.RequiresReview), cancellationToken);
        if (active is not null)
        {
            if (active.CustomerId != customerId || active.Amount != amount || active.Provider != "PayOS" || active.Currency != "VND")
                throw new BookingStatusConflictException("Existing checkout requires reconciliation.");
            await transaction.CommitAsync(cancellationToken);
            return (active, false);
        }
        var request = new PaymentRequests
        {
            BookingId = bookingId, CustomerId = customerId, Amount = amount,
            OrderCode = (long)RandomNumberGenerator.GetInt32(100000000, 999999999) * 1000000 + RandomNumberGenerator.GetInt32(1000000),
            CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc, ExpiresAtUtc = UtcTimestamp.ForResponse(booking.ExpiryDate)
        };
        context.PaymentRequests.Add(request);
        try { await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { throw new BookingStatusConflictException("A checkout was created concurrently. Retry the same booking."); }
        context.Entry(request).State = EntityState.Detached;
        return (request, true);
    }

    public async Task<PaymentRequests> UpdateAsync(int requestId, string status, string? linkId, string? checkoutUrl,
        string? error, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var bookingId = await context.PaymentRequests.Where(item => item.PaymentRequestId == requestId)
            .Select(item => item.BookingId).SingleAsync(cancellationToken);
        if (!bookingId.HasValue) throw new BookingStatusConflictException("Not a booking checkout.");
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await BookingPaymentLock.LockAsync(context, bookingId.Value, cancellationToken);
        var request = await context.PaymentRequests.SingleAsync(item => item.PaymentRequestId == requestId, cancellationToken);
        await context.Entry(request).ReloadAsync(cancellationToken);
        if (request.Status == PaymentRequestStatuses.Creating)
        {
            var valid = await context.Bookings.AnyAsync(item => item.BookingId == bookingId && item.Status == BookingStatuses.Pending &&
                item.CustomerId == request.CustomerId && item.DepositAmount == request.Amount && item.Contract == null, cancellationToken);
            request.Status = valid ? status : PaymentRequestStatuses.RequiresReview;
            request.PaymentLinkId = linkId ?? request.PaymentLinkId;
            request.CheckoutUrl = checkoutUrl ?? request.CheckoutUrl;
            request.LastErrorCode = valid ? error : "BOOKING_CHANGED_DURING_CREATION";
            request.UpdatedAtUtc = nowUtc;
            await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        context.Entry(request).State = EntityState.Detached;
        return request;
    }
}
