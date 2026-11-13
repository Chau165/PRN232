namespace PRN232.Application.Interfaces;

public record CheckoutLink(long OrderCode, decimal Amount, string Currency, string LinkId, string CheckoutUrl);
public record CheckoutStatus(string Status, decimal AmountPaid);
public interface IInvoiceCheckoutGateway
{
    Task<CheckoutLink> CreateAsync(long orderCode, decimal amount, DateTime expiresUtc, CancellationToken cancellationToken);
    Task<CheckoutStatus> GetStatusAsync(long orderCode, CancellationToken cancellationToken);
}

public sealed class CheckoutCreationRejectedException() : Exception("Provider rejected checkout creation.");
public sealed class CheckoutUnavailableException() : Exception("payOS checkout is not enabled.");
