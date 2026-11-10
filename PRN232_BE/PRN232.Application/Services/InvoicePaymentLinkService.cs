using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Settings;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class InvoicePaymentLinkService(IInvoicePaymentLinkRepository repository, IInvoiceCheckoutGateway gateway,
    PayOSSettings settings, TimeProvider timeProvider) : IInvoicePaymentLinkService
{
    public async Task<InvoicePaymentLinkDto> CreateAsync(int invoiceId, int customerId, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) throw new CheckoutUnavailableException();
        if (invoiceId <= 0) throw new InvoiceBusinessRuleException("InvoiceId must be positive.");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var (request, created) = await repository.ReserveAsync(invoiceId, customerId, now, now.AddMinutes(30), cancellationToken);
            if (!created)
            {
                if (request.Status == PaymentRequestStatuses.RequiresReview)
                    throw new InvoiceStatusLinkConflictException("This checkout requires reconciliation.");
                if (request.Status == PaymentRequestStatuses.Creating)
                    return Dto(request, true, false); // Never repeat an uncertain POST to payOS.
                if (request.ExpiresAtUtc > now && !string.IsNullOrEmpty(request.CheckoutUrl)) return Dto(request, true);
                CheckoutStatus remote;
                try { remote = await gateway.GetStatusAsync(request.OrderCode, cancellationToken); }
                catch (Exception) { return Dto(request, true, false); }
                var closedUnpaid = remote.AmountPaid == 0 && remote.Status is "CANCELLED" or "EXPIRED";
                var closedStatus = remote.Status == "CANCELLED" ? PaymentRequestStatuses.Cancelled : PaymentRequestStatuses.Expired;
                var updated = await Update(request, closedUnpaid ? closedStatus : PaymentRequestStatuses.RequiresReview,
                    null, null, closedUnpaid ? null : "EXPIRED_CHECKOUT_UNRESOLVED");
                if (updated.Status is PaymentRequestStatuses.Expired or PaymentRequestStatuses.Cancelled) continue;
                throw new InvoiceStatusLinkConflictException("The expired checkout requires reconciliation before another link can be created.");
            }

            CheckoutLink link;
            try
            {
                link = await gateway.CreateAsync(request.OrderCode, request.Amount, request.ExpiresAtUtc!.Value, cancellationToken);
            }
            catch (CheckoutCreationRejectedException)
            {
                await Update(request, PaymentRequestStatuses.CreateFailed, null, null, "CREATE_REJECTED");
                throw new InvoiceLinkProviderException();
            }
            catch (Exception)
            {
                // A timeout, cancellation or lost reply may follow successful provider creation.
                request = await Update(request, PaymentRequestStatuses.Creating, null, null, "CREATE_RESULT_UNKNOWN");
                return Dto(request, false, false);
            }
            var validUrl = Uri.TryCreate(link.CheckoutUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) && link.CheckoutUrl.Length <= 2048;
            if (link.OrderCode != request.OrderCode || link.Amount != request.Amount || link.Currency != "VND" ||
                string.IsNullOrWhiteSpace(link.LinkId) || link.LinkId.Length > 128 || !validUrl)
            {
                await Update(request, PaymentRequestStatuses.RequiresReview, null, null, "PROVIDER_RESPONSE_MISMATCH");
                throw new InvoiceStatusLinkConflictException("The provider response requires reconciliation.");
            }
            request = await Update(request, PaymentRequestStatuses.Pending, link.LinkId, link.CheckoutUrl, null);
            if (request.Status != PaymentRequestStatuses.Pending)
                throw new InvoiceStatusLinkConflictException("Invoice or checkout changed during creation; reconciliation is required.");
            return Dto(request, false);
        }
        throw new InvoiceStatusLinkConflictException("Please retry after checkout reconciliation.");
    }

    private async Task<PaymentRequests> Update(PaymentRequests request, string status, string? linkId, string? url, string? error)
    {
        // Persist the outcome even if the browser disconnected while payOS was processing.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await repository.UpdateAsync(request.PaymentRequestId, request.Status, status,
            linkId, url, error, timeProvider.GetUtcNow().UtcDateTime, cleanup.Token);
    }

    private static InvoicePaymentLinkDto Dto(PaymentRequests request, bool reused, bool includeUrl = true) => new(
        request.PaymentRequestId, request.InvoiceId, request.Status, request.Amount, request.Currency,
        includeUrl ? request.CheckoutUrl : null, request.ExpiresAtUtc, reused);
}

public sealed class InvoiceStatusLinkConflictException(string message) : Exception(message);
public sealed class InvoiceLinkProviderException() : Exception("payOS rejected checkout creation. Please check configuration or try again later.");
