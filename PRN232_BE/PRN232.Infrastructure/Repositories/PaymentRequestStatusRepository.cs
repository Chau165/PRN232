using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class PaymentRequestStatusRepository(AppDbContext context) : IPaymentRequestStatusRepository
{
    public Task<PaymentRequestStatusReadModel?> GetByIdAsync(int id, int? customerId, int? ownerId, CancellationToken cancellationToken) =>
        context.PaymentRequests.AsNoTracking()
            .Where(request => request.PaymentRequestId == id)
            .Where(request => customerId == null || request.CustomerId == customerId)
            .Where(request => ownerId == null || (request.InvoiceId.HasValue
                ? request.Invoice!.Contract!.Room!.Property!.OwnerId : request.Booking!.Room!.Property!.OwnerId) == ownerId)
            .Select(request => new PaymentRequestStatusReadModel(
                request.PaymentRequestId, request.InvoiceId, request.CustomerId,
                request.InvoiceId.HasValue ? request.Invoice!.Contract!.Room!.PropertyId : request.Booking!.Room!.PropertyId,
                request.Status, request.InvoiceId.HasValue ? request.Invoice!.Status : null, request.Amount, request.Currency,
                request.CreatedAtUtc, request.UpdatedAtUtc, request.ExpiresAtUtc, request.ConfirmedAtUtc,
                request.PaymentId, request.Payment == null ? null : request.Payment.PaidAtUtc, request.CheckoutUrl,
                request.BookingId, request.BookingId.HasValue ? request.Booking!.Status : null,
                request.BookingId.HasValue && request.Booking!.Contract != null))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken) =>
        context.PaymentRequests.AsNoTracking().AnyAsync(request => request.PaymentRequestId == id, cancellationToken);
}
