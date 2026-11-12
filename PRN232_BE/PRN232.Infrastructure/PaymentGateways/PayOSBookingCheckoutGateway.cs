using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using PRN232.Application.Interfaces;
using PRN232.Application.Settings;

namespace PRN232.Infrastructure.PaymentGateways;

public class PayOSBookingCheckoutGateway(PayOSClient client, IOptions<PayOSSettings> options) : IBookingCheckoutGateway
{
    public async Task<CheckoutLink> CreateAsync(long orderCode, decimal amount, DateTime expiresUtc, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.PaymentRequests.CreateAsync(new CreatePaymentLinkRequest
            {
                OrderCode = orderCode, Amount = checked((long)amount), Description = "COC PHONG", ReturnUrl = options.Value.ReturnUrl,
                CancelUrl = options.Value.CancelUrl, ExpiredAt = checked((int)new DateTimeOffset(DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc)).ToUnixTimeSeconds())
            }, new RequestOptions<CreatePaymentLinkRequest> { MaxRetries = 0, TimeoutMs = 20000, CancellationToken = cancellationToken });
            return new(response.OrderCode, response.Amount, response.Currency, response.PaymentLinkId, response.CheckoutUrl);
        }
        catch (Exception exception) when (exception is BadRequestException or UnauthorizedException or ForbiddenException)
        { throw new CheckoutCreationRejectedException(); }
    }
    public async Task<CheckoutStatus> GetStatusAsync(long orderCode, CancellationToken cancellationToken)
    {
        var response = await client.PaymentRequests.GetAsync(orderCode, new RequestOptions { MaxRetries = 0, TimeoutMs = 15000, CancellationToken = cancellationToken });
        return new(response.Status.ToString().ToUpperInvariant(), response.AmountPaid);
    }
}

public class DisabledBookingCheckoutGateway : IBookingCheckoutGateway
{
    public Task<CheckoutLink> CreateAsync(long orderCode, decimal amount, DateTime expiresUtc, CancellationToken cancellationToken) => throw new CheckoutUnavailableException();
    public Task<CheckoutStatus> GetStatusAsync(long orderCode, CancellationToken cancellationToken) => throw new CheckoutUnavailableException();
}
