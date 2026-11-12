using PRN232.Application.DTOs;
namespace PRN232.Application.Interfaces;

public interface IPaymentRequestStatusService
{
    Task<PaymentRequestStatusDto?> GetByIdAsync(int id, int userId, string role, CancellationToken cancellationToken);
}
