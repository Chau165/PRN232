using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class PayOSWebhookService(IPayOSWebhookVerifier verifier, IPayOSWebhookRepository repository, TimeProvider timeProvider) : IPayOSWebhookService
{
    public async Task ReceiveAsync(string json, CancellationToken cancellationToken)
    {
        var notice = await verifier.VerifyAsync(json);
        // payOS signs data, not the outer envelope. Do not let changing an outer
        // success flag poison an otherwise authentic transaction's receipt.
        if (notice.Code == "00" && (!notice.EnvelopeSuccess || notice.EnvelopeCode != "00"))
            throw new InvalidPayOSWebhookException();
        if (notice.OrderCode <= 0 || string.IsNullOrWhiteSpace(notice.Reference) || notice.Reference.Length > 256 ||
            string.IsNullOrWhiteSpace(notice.PaymentLinkId) || notice.PaymentLinkId.Length > 128 ||
            string.IsNullOrWhiteSpace(notice.Currency) || notice.Currency.Length > 16 ||
            notice.Code is null || notice.Code.Length > 32 || notice.EnvelopeCode is null || notice.EnvelopeCode.Length > 32 ||
            notice.TransactionDateTime?.Length > 64 || notice.Amount < -9999999999999999.99m || notice.Amount > 9999999999999999.99m)
            throw new InvalidPayOSWebhookException();
        var receipt = new PaymentWebhookReceipts
        {
            OrderCode = notice.OrderCode, PaymentLinkId = notice.PaymentLinkId, Amount = notice.Amount,
            Currency = notice.Currency, DataCode = notice.Code, Reference = notice.Reference,
            TransactionDateTime = notice.TransactionDateTime,
            Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                notice.OrderCode, notice.PaymentLinkId, notice.Amount, notice.Currency,
                notice.Code, notice.Reference, notice.TransactionDateTime
            })))),
            ReceivedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };
        await repository.RecordAsync(receipt,
            notice.EnvelopeSuccess && notice.EnvelopeCode == "00" && notice.Code == "00", cancellationToken);
    }
}
