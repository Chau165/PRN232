using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using PRN232.Application.Interfaces;
using PRN232.Application.Settings;

namespace PRN232.Infrastructure.PaymentGateways;

public class PayOSInvoiceCheckoutGateway(PayOSClient client, IOptions<PayOSSettings> options) : IInvoiceCheckoutGateway
{
    public async Task<CheckoutLink> CreateAsync(long orderCode, decimal amount, DateTime expiresUtc, CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var result = await client.PaymentRequests.CreateAsync(new CreatePaymentLinkRequest
            {
                OrderCode = orderCode, Amount = checked((long)amount), Description = "HOADON",
                ReturnUrl = settings.ReturnUrl, CancelUrl = settings.CancelUrl,
                ExpiredAt = checked((int)new DateTimeOffset(expiresUtc).ToUnixTimeSeconds())
            }, new RequestOptions<CreatePaymentLinkRequest> { MaxRetries = 0, TimeoutMs = 20000, CancellationToken = cancellationToken });
            return new CheckoutLink(result.OrderCode, result.Amount, result.Currency, result.PaymentLinkId, result.CheckoutUrl);
        }
        catch (Exception exception) when (exception is BadRequestException or UnauthorizedException or ForbiddenException)
        {
            // Do not expose provider responses or credentials to the caller.
            throw new CheckoutCreationRejectedException();
        }
    }

    public async Task<CheckoutStatus> GetStatusAsync(long orderCode, CancellationToken cancellationToken)
    {
        var result = await client.PaymentRequests.GetAsync(orderCode,
            new RequestOptions { MaxRetries = 0, TimeoutMs = 15000, CancellationToken = cancellationToken });
        return new CheckoutStatus(result.Status.ToString().ToUpperInvariant(), result.AmountPaid);
    }
}

public class DisabledInvoiceCheckoutGateway : IInvoiceCheckoutGateway
{
    public Task<CheckoutLink> CreateAsync(long orderCode, decimal amount, DateTime expiresUtc, CancellationToken cancellationToken) =>
        throw new CheckoutUnavailableException();
    public Task<CheckoutStatus> GetStatusAsync(long orderCode, CancellationToken cancellationToken) =>
        throw new CheckoutUnavailableException();
}
