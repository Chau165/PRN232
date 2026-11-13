using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPropertyManagerAssignmentService
{
    Task<IReadOnlyList<PropertyManagerAssignmentDto>> GetAssignmentsByPropertyAsync(
        int propertyId,
        string role,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PropertyManagersDto>> GetAssignmentsForPropertiesAsync(
        IReadOnlyCollection<int>? propertyIds,
        string role,
        CancellationToken cancellationToken);
    Task<PropertyManagerAssignmentDto> AssignManagerAsync(
        int propertyId,
        int managerId,
        int adminId,
        string role,
        CancellationToken cancellationToken);
    Task RevokeManagerAsync(
        int propertyId,
        int managerId,
        int adminId,
        string role,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PropertyManagerAssignmentDto>> GetAssignmentsByManagerAsync(
        int managerId,
        int currentUserId,
        string role,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PropertyDto>> GetMyManagedPropertiesAsync(
        int managerId,
        string role,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PropertyDto>> GetPropertiesForManagerAsync(
        int managerId,
        int currentUserId,
        string role,
        CancellationToken cancellationToken);
}
