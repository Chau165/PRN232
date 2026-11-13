using PayOS;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using PRN232.Application.Interfaces;

namespace PRN232.Infrastructure.PaymentGateways;

public class PayOSCancellationGateway(PayOSClient client) : IPayOSCancellationGateway
{
    public async Task CancelAsync(long orderCode, CancellationToken cancellationToken) =>
        await client.PaymentRequests.CancelAsync(orderCode, "Switch to cash collection",
            new RequestOptions<CancelPaymentLinkRequest> { MaxRetries = 0, TimeoutMs = 15000, CancellationToken = cancellationToken });
}

public class DisabledPayOSCancellationGateway : IPayOSCancellationGateway
{
    public Task CancelAsync(long orderCode, CancellationToken cancellationToken) => throw new CheckoutUnavailableException();
}
