using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

// P5 exposes only balance/estimation: no method can create a payout or change financial state.
public interface IPayOSPayoutGateway
{
    Task<PayoutBalanceSnapshot> GetBalanceAsync(CancellationToken cancellationToken);
    Task<PayoutCostEstimate> EstimateAsync(PayoutPreflightRequest request, CancellationToken cancellationToken);
    Task<PayoutPreflightSnapshot> PreflightAsync(PayoutPreflightRequest request, CancellationToken cancellationToken);
}

public sealed class PayOSPayoutDisabledException() : Exception("The payOS payout channel is not enabled.");
public sealed class PayOSPayoutValidationException(string message) : Exception(message);
public sealed class PayOSPayoutGatewayException(string code, bool retryable = false) : Exception("Unable to verify payOS payout information.")
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}
