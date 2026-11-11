using System.Globalization;
using Microsoft.Extensions.Options;
using PayOS.Exceptions;
using PayOS.Models;
using PayOS.Models.V1.Payouts.Batch;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.Settings;

namespace PRN232.Infrastructure.PaymentGateways;

public class PayOSPayoutGateway(PayOSPayoutClient payout, IOptions<PayOSPayoutSettings> options, TimeProvider clock) : IPayOSPayoutGateway
{
    public Task<PayoutBalanceSnapshot> GetBalanceAsync(CancellationToken cancellationToken) => GuardAsync(async () =>
    {
        var response = await payout.Client.PayoutsAccount.GetBalanceAsync(Request(cancellationToken));
        if (response.Currency != "VND" || !decimal.TryParse(response.Balance, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var balance) || balance < 0)
            throw new PayOSPayoutGatewayException("PAYOUT_BALANCE_INVALID");
        return new PayoutBalanceSnapshot(balance, response.Currency, clock.GetUtcNow().UtcDateTime);
    }, cancellationToken);

    public Task<PayoutCostEstimate> EstimateAsync(PayoutPreflightRequest request, CancellationToken cancellationToken)
    {
        PayoutPreflightValidation.Validate(request);
        return GuardAsync(async () =>
        {
            // This is a one-item quote, not Batch.CreateAsync. It never issues a transfer.
            var response = await payout.Client.Payouts.EstimateCredit(new PayoutBatchRequest
            {
                ReferenceId = request.ReferenceId, ValidateDestination = options.Value.ValidateDestinationOnEstimate,
                Payouts = [new PayoutBatchItem { ReferenceId = request.ReferenceId, Amount = checked((long)request.Amount),
                    Description = request.Description, ToBin = request.RecipientBankBin, ToAccountNumber = request.RecipientAccountNumber }]
            }, Request(cancellationToken));
            if (response.EstimateCreditAmount < 0) throw new PayOSPayoutGatewayException("PAYOUT_ESTIMATE_INVALID");
            return new PayoutCostEstimate(response.EstimateCreditAmount, options.Value.ValidateDestinationOnEstimate, clock.GetUtcNow().UtcDateTime);
        }, cancellationToken);
    }

    public async Task<PayoutPreflightSnapshot> PreflightAsync(PayoutPreflightRequest request, CancellationToken cancellationToken)
    {
        PayoutPreflightValidation.Validate(request);
        var balance = GetBalanceAsync(cancellationToken);
        var cost = EstimateAsync(request, cancellationToken);
        await Task.WhenAll(balance, cost);
        return new(await balance, await cost, (await balance).Balance >= request.Amount);
    }

    private RequestOptions Request(CancellationToken cancellationToken) => new()
    {
        MaxRetries = 0, TimeoutMs = options.Value.TimeoutMilliseconds, CancellationToken = cancellationToken
    };

    private async Task<T> GuardAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) throw new PayOSPayoutDisabledException();
        cancellationToken.ThrowIfCancellationRequested();
        try { return await operation(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        // The SDK can wrap HTTP cancellation in its own API exception.
        catch (Exception) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException(cancellationToken); }
        catch (PayOSPayoutGatewayException) { throw; }
        catch (InvalidSignatureException) { throw new PayOSPayoutGatewayException("PAYOUT_RESPONSE_INVALID"); }
        catch (Exception exception) when (exception is UnauthorizedException or ForbiddenException)
        { throw new PayOSPayoutGatewayException("PAYOUT_ACCESS_DENIED"); }
        catch (BadRequestException) { throw new PayOSPayoutGatewayException("PAYOUT_PREFLIGHT_REJECTED"); }
        catch (Exception) { throw new PayOSPayoutGatewayException("PAYOUT_LOOKUP_UNAVAILABLE", true); }
    }
}

public class DisabledPayOSPayoutGateway : IPayOSPayoutGateway
{
    public Task<PayoutBalanceSnapshot> GetBalanceAsync(CancellationToken cancellationToken) => throw new PayOSPayoutDisabledException();
    public Task<PayoutCostEstimate> EstimateAsync(PayoutPreflightRequest request, CancellationToken cancellationToken) => throw new PayOSPayoutDisabledException();
    public Task<PayoutPreflightSnapshot> PreflightAsync(PayoutPreflightRequest request, CancellationToken cancellationToken) => throw new PayOSPayoutDisabledException();
}
