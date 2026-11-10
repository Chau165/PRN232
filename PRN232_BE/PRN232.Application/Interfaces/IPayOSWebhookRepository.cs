using PRN232.Domain.Entities;
namespace PRN232.Application.Interfaces;

public interface IPayOSWebhookRepository
{
    Task RecordAsync(PaymentWebhookReceipts receipt, bool providerSuccess, CancellationToken cancellationToken);
}
