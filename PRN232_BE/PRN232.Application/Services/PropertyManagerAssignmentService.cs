using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public sealed class PropertyManagerAssignmentService : IPropertyManagerAssignmentService
{
    private readonly IPropertyManagerAssignmentRepository _repository;

    public PropertyManagerAssignmentService(IPropertyManagerAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PropertyManagerAssignmentDto>> GetAssignmentsByPropertyAsync(
        int propertyId,
        string role,
        CancellationToken cancellationToken)
    {
        EnsureAdmin(role);
        if (!await _repository.PropertyExistsAsync(propertyId, cancellationToken))
        {
            throw new PropertyManagerAssignmentNotFoundException("Không tìm thấy khu trọ.");
        }
        var assignments = await _repository.GetByPropertyIdAsync(
            propertyId,
            cancellationToken);

        return assignments.Select(ConvertToDto).ToArray();
    }

    public async Task<IReadOnlyList<PropertyManagersDto>> GetAssignmentsForPropertiesAsync(
        IReadOnlyCollection<int>? propertyIds,
        string role,
        CancellationToken cancellationToken)
    {
        EnsureAdmin(role);
        if (propertyIds is { Count: 0 })
        {
            return [];
        }

        var assignments = await _repository.GetByPropertyIdsAsync(propertyIds, cancellationToken);
        return assignments
            .GroupBy(assignment => assignment.PropertyId)
            .OrderBy(group => group.Key)
            .Select(group => new PropertyManagersDto(
                group.Key,
                group.Select(ConvertToDto).ToArray()))
            .ToArray();
    }

    public async Task<PropertyManagerAssignmentDto> AssignManagerAsync(
        int propertyId,
        int managerId,
        int adminId,
        string role,
        CancellationToken cancellationToken)
    {
        await EnsurePropertyOwnerAsync(propertyId, adminId, role, cancellationToken);
        if (!await _repository.IsActiveManagerAsync(managerId, cancellationToken))
        {
            throw new PropertyManagerAssignmentNotFoundException(
                "Không tìm thấy Manager đang hoạt động.");
        }

        var assignment = ConvertToEntity(propertyId, managerId);

        var created = await _repository.AddIfActiveAssignmentAbsentAsync(
            assignment,
            adminId,
            cancellationToken);
        if (created is null)
        {
            await EnsurePropertyOwnerAsync(propertyId, adminId, role, cancellationToken);

            if (!await _repository.IsActiveManagerAsync(managerId, cancellationToken))
            {
                throw new PropertyManagerAssignmentNotFoundException(
                    "Không tìm thấy Manager đang hoạt động.");
            }

            throw new PropertyManagerAssignmentConflictException(
                "Manager đã được phân công cho khu trọ này.");
        }

        return ConvertToDto(created);
    }

    public async Task RevokeManagerAsync(
        int propertyId,
        int managerId,
        int adminId,
        string role,
        CancellationToken cancellationToken)
    {
        await EnsurePropertyOwnerAsync(propertyId, adminId, role, cancellationToken);
        var result = await _repository.RevokeActiveAsync(
            propertyId,
            managerId,
            adminId,
            cancellationToken);

        if (result == RevokePropertyManagerAssignmentResult.NotOwned)
        {
            await EnsurePropertyOwnerAsync(propertyId, adminId, role, cancellationToken);
        }

        if (result == RevokePropertyManagerAssignmentResult.NotFound)
        {
            throw new PropertyManagerAssignmentNotFoundException(
                "Không tìm thấy phân công Manager đang hoạt động.");
        }

        if (result == RevokePropertyManagerAssignmentResult.HasOpenWork)
        {
            throw new PropertyManagerAssignmentConflictException(
                "Không thể thu hồi khi Manager còn lịch xem, hợp đồng đang hiệu lực hoặc yêu cầu bảo trì đang xử lý. Hãy bàn giao công việc trước.");
        }
    }

    public async Task<IReadOnlyList<PropertyManagerAssignmentDto>> GetAssignmentsByManagerAsync(
        int managerId,
        int currentUserId,
        string role,
        CancellationToken cancellationToken)
    {
        if (role != RoleNames.Admin &&
            (role != RoleNames.Manager || managerId != currentUserId))
        {
            throw new PropertyManagerAssignmentAuthorizationException(
                "Bạn không có quyền xem phân công của Manager này.");
        }

        var assignments = await _repository.GetByManagerIdAsync(
            managerId,
            requireActiveManager: role == RoleNames.Manager,
            cancellationToken);
        return assignments.Select(ConvertToDto).ToArray();
    }

    public async Task<IReadOnlyList<PropertyDto>> GetMyManagedPropertiesAsync(
        int managerId,
        string role,
        CancellationToken cancellationToken)
    {
        if (role != RoleNames.Manager)
        {
            throw new PropertyManagerAssignmentAuthorizationException(
                "Chỉ Manager mới được xem các khu trọ được phân công.");
        }

        var properties = await _repository.GetManagedPropertiesAsync(
            managerId,
            requireActiveManager: true,
            cancellationToken);
        return properties.Select(ConvertToDto).ToArray();
    }

    public async Task<IReadOnlyList<PropertyDto>> GetPropertiesForManagerAsync(
        int managerId,
        int currentUserId,
        string role,
        CancellationToken cancellationToken)
    {
        if (role != RoleNames.Admin &&
            (role != RoleNames.Manager || managerId != currentUserId))
        {
            throw new PropertyManagerAssignmentAuthorizationException(
                "Bạn không có quyền xem các khu trọ của Manager này.");
        }

        var properties = await _repository.GetManagedPropertiesAsync(
            managerId,
            requireActiveManager: role == RoleNames.Manager,
            cancellationToken);
        return properties.Select(ConvertToDto).ToArray();
    }

    private async Task EnsurePropertyOwnerAsync(
        int propertyId,
        int adminId,
        string role,
        CancellationToken cancellationToken)
    {
        EnsureAdmin(role);

        if (!await _repository.PropertyExistsAsync(propertyId, cancellationToken))
        {
            throw new PropertyManagerAssignmentNotFoundException(
                "Không tìm thấy khu trọ.");
        }

        var propertyOwner = await _repository.GetPropertyOwnerAsync(propertyId, cancellationToken);
        if (propertyOwner is null)
        {
            throw new PropertyManagerAssignmentNotFoundException("Không tìm thấy khu trọ.");
        }

        if (propertyOwner.OwnerId != adminId)
        {
            if (!string.Equals(
                    propertyOwner.OwnerRoleName,
                    RoleNames.Admin,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new PropertyManagerAssignmentAuthorizationException(
                    $"Khu trọ có OwnerId={propertyOwner.OwnerId} đang trỏ tới tài khoản {propertyOwner.OwnerRoleName} \"{propertyOwner.OwnerName}\", không phải Admin. Hãy xác nhận Admin sở hữu hợp lệ và mapping OwnerId trước khi phân công; OwnerId hiện tại không bị thay đổi.");
            }

            throw new PropertyManagerAssignmentAuthorizationException(
                $"Khu trọ thuộc Admin \"{propertyOwner.OwnerName}\" (ID: {propertyOwner.OwnerId}); chỉ Admin sở hữu mới được thay đổi phân công.");
        }
    }

    private static void EnsureAdmin(string role)
    {
        if (role != RoleNames.Admin)
        {
            throw new PropertyManagerAssignmentAuthorizationException(
                "Chỉ Admin mới được truy cập chức năng phân công Manager.");
        }
    }

    private static PropertyManagerAssignmentDto ConvertToDto(
        PropertyManagerAssignmentReadModel assignment)
    {
        return new PropertyManagerAssignmentDto(
            assignment.PropertyId,
            assignment.PropertyName,
            assignment.ManagerId,
            assignment.ManagerName,
            assignment.ManagerEmail,
            assignment.ManagerIsActive);
    }

    private static PropertyManagerAssignment ConvertToEntity(int propertyId, int managerId)
    {
        return new PropertyManagerAssignment
        {
            PropertyId = propertyId,
            ManagerId = managerId
        };
    }

    private static PropertyDto ConvertToDto(PropertySummaryReadModel property)
    {
        return new PropertyDto(
            property.PropertyId,
            property.OwnerId,
            property.OwnerName,
            property.PropertyName,
            property.AddressText,
            property.Ward,
            property.District,
            property.City,
            property.Latitude,
            property.Longitude,
            property.RoomCount,
            property.PrimaryImageUrl,
            property.Description);
    }
}

public sealed class PropertyManagerAssignmentAuthorizationException(string message)
    : Exception(message);

public sealed class PropertyManagerAssignmentNotFoundException(string message)
    : Exception(message);

public sealed class PropertyManagerAssignmentConflictException(string message)
    : Exception(message);
