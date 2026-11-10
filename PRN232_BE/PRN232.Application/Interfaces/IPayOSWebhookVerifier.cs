namespace PRN232.Application.Interfaces;

public record VerifiedPayOSNotice(long OrderCode, string PaymentLinkId, decimal Amount, string Currency,
    string Code, string Reference, string? TransactionDateTime, bool EnvelopeSuccess, string EnvelopeCode);
public interface IPayOSWebhookVerifier
{
    Task<VerifiedPayOSNotice> VerifyAsync(string json);
}
public sealed class InvalidPayOSWebhookException() : Exception("Invalid payOS webhook.");
