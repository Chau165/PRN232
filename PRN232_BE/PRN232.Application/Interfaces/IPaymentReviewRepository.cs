using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

public interface IPaymentReviewRepository
{
    Task<List<PaymentReviewReadModel>> GetAllAsync(string status, int skip, int take, CancellationToken cancellationToken);
    Task<PaymentReviewReadModel?> GetAsync(int id, CancellationToken cancellationToken);
    Task<bool> ResolveAsync(int id, int adminId, string resolution, string note, string? evidence,
        DateTime nowUtc, CancellationToken cancellationToken);
}
