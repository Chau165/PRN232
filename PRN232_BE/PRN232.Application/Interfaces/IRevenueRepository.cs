using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

public interface IRevenueRepository
{
    Task<int?> GetPropertyOwnerAsync(int propertyId, CancellationToken cancellationToken);
    Task<bool> IsManagerAssignedAsync(int propertyId, int managerId, CancellationToken cancellationToken);
    Task<List<RevenueBucketReadModel>> GetBucketsAsync(int? propertyId, int? ownerId,
        DateTime fromUtc, DateTime untilUtc, CancellationToken cancellationToken);
}
