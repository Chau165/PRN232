using Microsoft.Extensions.Options;
using PayOS.Models;
using PayOS.Models.V1.Payouts;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.Settings;

namespace PRN232.Infrastructure.PaymentGateways;

public sealed class PayOSPayoutSubmissionGateway(PayOSPayoutClient payout, IOptions<PayOSPayoutSettings> options) : IPayOSPayoutSubmissionGateway
{
    public async Task<PayoutSubmissionReceipt> SubmitAsync(PayoutSubmissionRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !options.Value.DispatchEnabled) throw new PayOSPayoutDisabledException();
        PayoutPreflightValidation.Validate(new(request.ReferenceId, request.Amount, request.RecipientBankBin,
            request.RecipientAccountNumber, request.Description));
        if (!Guid.TryParseExact(request.IdempotencyKey, "N", out _))
            throw new PayOSPayoutValidationException("A persisted payout idempotency key is required.");
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var result = await payout.Client.Payouts.CreateAsync(new PayoutRequest
            {
                ReferenceId = request.ReferenceId, Amount = checked((long)request.Amount), Description = request.Description,
                ToBin = request.RecipientBankBin, ToAccountNumber = request.RecipientAccountNumber
            }, request.IdempotencyKey, new RequestOptions<Payout>
            {
                MaxRetries = 0, TimeoutMs = options.Value.TimeoutMilliseconds, CancellationToken = cancellationToken
            });
            var transaction = result.Transactions?.SingleOrDefault();
            if (!Identifier(result.Id) || result.ReferenceId != request.ReferenceId || transaction is null ||
                !Identifier(transaction.Id) || transaction.ReferenceId != request.ReferenceId || transaction.Amount != request.Amount ||
                transaction.ToBin != request.RecipientBankBin || transaction.ToAccountNumber != request.RecipientAccountNumber ||
                !Enum.IsDefined(result.ApprovalState) || !Enum.IsDefined(transaction.State))
                throw new PayOSPayoutGatewayException("PAYOUT_RESPONSE_MISMATCH");
            return new(result.Id, transaction.Id, result.ApprovalState.ToString(), transaction.State.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (PayOSPayoutGatewayException) { throw; }
        catch (Exception) { throw new PayOSPayoutGatewayException("PAYOUT_SUBMISSION_UNCONFIRMED"); }
    }

    private static bool Identifier(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value == value.Trim() && !value.Any(char.IsControl);
}

public sealed class DisabledPayOSPayoutSubmissionGateway : IPayOSPayoutSubmissionGateway
{
    public Task<PayoutSubmissionReceipt> SubmitAsync(PayoutSubmissionRequest request, CancellationToken cancellationToken) =>
        throw new PayOSPayoutDisabledException();
}
