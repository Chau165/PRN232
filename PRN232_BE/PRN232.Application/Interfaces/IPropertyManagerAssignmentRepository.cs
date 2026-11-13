using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IPropertyManagerAssignmentRepository
{
    Task<bool> PropertyExistsAsync(int propertyId, CancellationToken cancellationToken);
    Task<bool> IsOwnedByAdminAsync(int propertyId, int adminId, CancellationToken cancellationToken);
    Task<PropertyOwnerReadModel?> GetPropertyOwnerAsync(
        int propertyId,
        CancellationToken cancellationToken);
    Task<bool> IsActiveManagerAsync(int managerId, CancellationToken cancellationToken);
    Task<PropertyManagerAssignmentReadModel?> AddIfActiveAssignmentAbsentAsync(
        PropertyManagerAssignment assignment,
        int adminId,
        CancellationToken cancellationToken);
    Task<List<PropertyManagerAssignmentReadModel>> GetByPropertyIdAsync(
        int propertyId,
        CancellationToken cancellationToken);
    /// <summary>
    /// Loads assignments for many properties in one query. A null filter returns every assignment.
    /// </summary>
    Task<List<PropertyManagerAssignmentReadModel>> GetByPropertyIdsAsync(
        IReadOnlyCollection<int>? propertyIds,
        CancellationToken cancellationToken);
    Task<List<PropertyManagerAssignmentReadModel>> GetByManagerIdAsync(
        int managerId,
        bool requireActiveManager,
        CancellationToken cancellationToken);
    Task<List<PropertySummaryReadModel>> GetManagedPropertiesAsync(
        int managerId,
        bool requireActiveManager,
        CancellationToken cancellationToken);
    Task<RevokePropertyManagerAssignmentResult> RevokeActiveAsync(
        int propertyId,
        int managerId,
        int adminId,
        CancellationToken cancellationToken);
}

public enum RevokePropertyManagerAssignmentResult
{
    Revoked,
    NotOwned,
    NotFound,
    HasOpenWork
}
