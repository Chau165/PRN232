using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

public interface IReceivablesRepository
{
    Task<int?> GetPropertyOwnerAsync(int propertyId, CancellationToken cancellationToken);
    Task<bool> IsManagerAssignedAsync(int propertyId, int managerId, CancellationToken cancellationToken);
    Task<List<ReceivableReadModel>> GetOutstandingAsync(int? propertyId, int? ownerId, CancellationToken cancellationToken);
}
