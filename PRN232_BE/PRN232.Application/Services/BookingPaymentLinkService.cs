using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.Settings;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class BookingPaymentLinkService(IBookingPaymentLinkRepository repository, IBookingCheckoutGateway gateway,
    PayOSSettings settings, TimeProvider clock) : IBookingPaymentLinkService
{
    public async Task<BookingPaymentLinkDto> CreateAsync(int bookingId, int customerId, CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !settings.BookingPaymentsEnabled) throw new CheckoutUnavailableException();
        if (bookingId <= 0) throw new BookingBusinessRuleException("BookingId must be positive.");
        var (request, created) = await repository.ReserveAsync(bookingId, customerId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        if (!created)
        {
            if (request.Status == PaymentRequestStatuses.RequiresReview)
                throw new BookingStatusConflictException("This checkout requires reconciliation before another payment.");
            return Dto(request, true);
        }

        CheckoutLink link;
        try { link = await gateway.CreateAsync(request.OrderCode, request.Amount, request.ExpiresAtUtc!.Value, cancellationToken); }
        catch (CheckoutCreationRejectedException)
        {
            await Update(PaymentRequestStatuses.CreateFailed, null, null, "CREATE_REJECTED");
            throw new BookingLinkProviderException();
        }
        catch (Exception)
        {
            request = await Update(PaymentRequestStatuses.Creating, null, null, "CREATE_RESULT_UNKNOWN");
            return Dto(request, false);
        }
        var validUrl = Uri.TryCreate(link.CheckoutUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
            string.IsNullOrEmpty(uri.UserInfo) && link.CheckoutUrl.Length <= 2048;
        if (link.OrderCode != request.OrderCode || link.Amount != request.Amount || link.Currency != "VND" ||
            string.IsNullOrWhiteSpace(link.LinkId) || link.LinkId.Length > 128 || !validUrl)
        {
            await Update(PaymentRequestStatuses.RequiresReview, null, null, "PROVIDER_RESPONSE_MISMATCH");
            throw new BookingStatusConflictException("The provider response requires reconciliation.");
        }
        request = await Update(PaymentRequestStatuses.Pending, link.LinkId, link.CheckoutUrl, null);
        if (request.Status is not (PaymentRequestStatuses.Pending or PaymentRequestStatuses.Succeeded))
            throw new BookingStatusConflictException("Booking or checkout changed during creation. Check payment status.");
        return Dto(request, false);

        async Task<PaymentRequests> Update(string status, string? id, string? url, string? error)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return await repository.UpdateAsync(request.PaymentRequestId, status, id, url, error, clock.GetUtcNow().UtcDateTime, cleanup.Token);
        }
    }

    private BookingPaymentLinkDto Dto(PaymentRequests request, bool reused) => new(request.PaymentRequestId,
        request.BookingId!.Value, request.Status, request.Amount, request.Currency,
        request.Status == PaymentRequestStatuses.Pending && request.ExpiresAtUtc > clock.GetUtcNow().UtcDateTime ? request.CheckoutUrl : null,
        UtcTimestamp.ForResponse(request.ExpiresAtUtc), reused);
}

public sealed class BookingLinkProviderException() : Exception("payOS rejected booking checkout creation.");
