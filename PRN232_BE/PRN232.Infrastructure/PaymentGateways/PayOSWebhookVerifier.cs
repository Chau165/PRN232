using System.Text.Json;
using System.Security.Cryptography;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models.Webhooks;
using PRN232.Application.Interfaces;

namespace PRN232.Infrastructure.PaymentGateways;

public class PayOSWebhookVerifier(PayOSClient client) : IPayOSWebhookVerifier
{
    public async Task<VerifiedPayOSNotice> VerifyAsync(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("signature", out var signature) || signature.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(signature.GetString()) ||
                !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("success", out var success) || success.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !root.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.String)
                throw new InvalidPayOSWebhookException();
            if (!data.TryGetProperty("orderCode", out var order) || !order.TryGetInt64(out _) ||
                !data.TryGetProperty("amount", out var amount) || !amount.TryGetInt64(out _) ||
                !data.TryGetProperty("paymentLinkId", out var link) || link.ValueKind != JsonValueKind.String ||
                !data.TryGetProperty("currency", out var currency) || currency.ValueKind != JsonValueKind.String ||
                !data.TryGetProperty("reference", out var reference) || reference.ValueKind != JsonValueKind.String ||
                !data.TryGetProperty("code", out var dataCode) || dataCode.ValueKind != JsonValueKind.String)
                throw new InvalidPayOSWebhookException();
            var webhook = JsonSerializer.Deserialize<Webhook>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (webhook is null) throw new InvalidPayOSWebhookException();
            // Official SDK validates the data signature locally; no outbound API call.
            var verified = await client.Webhooks.VerifyAsync(webhook);
            return new VerifiedPayOSNotice(verified.OrderCode, verified.PaymentLinkId, verified.Amount,
                verified.Currency, verified.Code, verified.Reference, verified.TransactionDateTime,
                success.GetBoolean(), code.GetString()!);
        }
        catch (Exception exception) when (exception is JsonException or PayOSException or ArgumentException or
            FormatException or CryptographicException or InvalidOperationException)
        {
            throw new InvalidPayOSWebhookException();
        }
    }
}

public class DisabledPayOSWebhookVerifier : IPayOSWebhookVerifier
{
    public Task<VerifiedPayOSNotice> VerifyAsync(string json) => throw new CheckoutUnavailableException();
}
