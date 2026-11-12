using PayOS;

namespace PRN232.Infrastructure.PaymentGateways;

// Distinct DI type prevents a second PayOSClient registration replacing the incoming channel client.
public sealed class PayOSPayoutClient(PayOSClient client)
{
    internal PayOSClient Client { get; } = client;
}
