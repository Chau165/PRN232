using Microsoft.Extensions.Options;
using PayOS.Exceptions;
using PayOS.Models;
using PayOS.Models.V1.Payouts;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Settings;

namespace PRN232.Infrastructure.PaymentGateways;

public sealed class PayOSPayoutReconciliationGateway(PayOSPayoutClient payout, IOptions<PayOSPayoutSettings> options) : IPayOSPayoutReconciliationGateway
{
    public async Task<PayoutRemoteSnapshot?> GetAsync(PayoutLookupRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !options.Value.ReconciliationEnabled) throw new PayOSPayoutDisabledException();
        if (!Identifier(request.ReferenceId) || request.ProviderPayoutId is not null && !Identifier(request.ProviderPayoutId))
            throw new PayOSPayoutValidationException("Valid persisted payout identifiers are required.");
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Payout? result;
            if (request.ProviderPayoutId is { } id)
                result = await payout.Client.Payouts.GetAsync(id, Request(cancellationToken));
            else
            {
                var page = await payout.Client.Payouts.ListAsync(new GetPayoutListParam
                { ReferenceId = request.ReferenceId, Limit = 2, Offset = 0 }, Request(cancellationToken));
                // Never take the first item from ambiguous/truncated results, even if one reference matches.
                if (page.Data.Count > 1 || page.HasNextPage || page.Pagination.Total > 1)
                    throw new PayOSPayoutGatewayException("PAYOUT_LOOKUP_AMBIGUOUS");
                result = page.Data.SingleOrDefault();
            }
            if (result is null) return null;
            var transaction = result.Transactions?.SingleOrDefault();
            if (!Identifier(result.Id) || !Identifier(transaction?.Id) || transaction is null ||
                result.ReferenceId != request.ReferenceId || transaction.ReferenceId != request.ReferenceId ||
                request.ProviderPayoutId is not null && request.ProviderPayoutId != result.Id ||
                !Enum.IsDefined(result.ApprovalState) || !Enum.IsDefined(transaction.State))
                throw new PayOSPayoutGatewayException("PAYOUT_LOOKUP_MISMATCH");
            return new(result.Id, result.ReferenceId, transaction.Id, transaction.ReferenceId, transaction.Amount,
                transaction.ToBin, transaction.ToAccountNumber, result.ApprovalState.ToString(), transaction.State.ToString(),
                transaction.Reference, transaction.TransactionDatetime, transaction.ErrorCode);
        }
        catch (NotFoundException) { return null; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException(cancellationToken); }
        catch (PayOSPayoutGatewayException) { throw; }
        catch (Exception) { throw new PayOSPayoutGatewayException("PAYOUT_LOOKUP_UNAVAILABLE"); }
    }

    private RequestOptions Request(CancellationToken cancellationToken) => new()
    { MaxRetries = 0, TimeoutMs = options.Value.TimeoutMilliseconds, CancellationToken = cancellationToken };
    private static bool Identifier(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
}

public sealed class DisabledPayOSPayoutReconciliationGateway : IPayOSPayoutReconciliationGateway
{
    public Task<PayoutRemoteSnapshot?> GetAsync(PayoutLookupRequest request, CancellationToken cancellationToken) => throw new PayOSPayoutDisabledException();
}
