using PRN232.Application.ReadModels;
namespace PRN232.Application.Interfaces;

public interface IPaymentRequestStatusRepository
{
    Task<PaymentRequestStatusReadModel?> GetByIdAsync(int id, int? customerId, int? managerId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken);
}
