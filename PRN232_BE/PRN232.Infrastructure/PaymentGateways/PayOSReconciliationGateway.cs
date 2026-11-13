using PayOS;
using PayOS.Exceptions;
using PayOS.Models;
using PRN232.Application.Interfaces;

namespace PRN232.Infrastructure.PaymentGateways;

public class PayOSReconciliationGateway(PayOSClient client) : IPayOSReconciliationGateway
{
    public async Task<PayOSRemoteSnapshot?> GetAsync(long orderCode, CancellationToken cancellationToken)
    {
        try
        {
            var result = await client.PaymentRequests.GetAsync(orderCode,
                new RequestOptions { MaxRetries = 0, TimeoutMs = 15000, CancellationToken = cancellationToken });
            return new(result.OrderCode, result.Id, result.Amount, result.AmountPaid, result.AmountRemaining,
                result.Status.ToString().ToUpperInvariant(), (result.Transactions ?? []).Select(item =>
                    new PayOSRemoteTransfer(item.Reference, item.Amount, item.TransactionDateTime)).ToList());
        }
        catch (NotFoundException) { return null; }
    }
}

public class DisabledPayOSReconciliationGateway : IPayOSReconciliationGateway
{
    public Task<PayOSRemoteSnapshot?> GetAsync(long orderCode, CancellationToken cancellationToken) =>
        throw new CheckoutUnavailableException();
}
