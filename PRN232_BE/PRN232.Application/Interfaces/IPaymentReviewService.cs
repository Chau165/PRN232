using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPaymentReviewService
{
    Task<List<PaymentReviewDto>> GetAllAsync(int userId, string role, string status, int page, int pageSize, CancellationToken cancellationToken);
    Task<PaymentReviewDto?> GetAsync(int id, int userId, string role, CancellationToken cancellationToken);
    Task<PaymentReviewDto> ResolveAsync(int id, int userId, string role, ResolvePaymentReviewDto dto, CancellationToken cancellationToken);
}
