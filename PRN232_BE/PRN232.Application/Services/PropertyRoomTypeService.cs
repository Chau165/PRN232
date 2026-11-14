using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public sealed class PropertyRoomTypeService : IPropertyRoomTypeService
{
    private readonly IPropertyRoomTypeRepository _repository;
    private readonly TimeProvider _timeProvider;

    public PropertyRoomTypeService(IPropertyRoomTypeRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<List<PropertyRoomTypeDto>> GetAllByPropertyIdAsync(
        int propertyId,
        int requestingUserId,
        string role)
    {
        await EnsurePropertyExistsAsync(propertyId);
        await EnsureCanReadPropertyAsync(propertyId, requestingUserId, role);
        var items = await _repository.GetAllByPropertyIdAsync(propertyId, _timeProvider.GetUtcNow());
        return items.Select(ConvertToDto).ToList();
    }

    public async Task<PropertyRoomTypeDto?> GetByIdAsync(
        int propertyId,
        int roomTypeId,
        int requestingUserId,
        string role)
    {
        await EnsurePropertyExistsAsync(propertyId);
        await EnsureCanReadPropertyAsync(propertyId, requestingUserId, role);
        var item = await _repository.GetByIdAsync(propertyId, roomTypeId, _timeProvider.GetUtcNow());
        return item is null ? null : ConvertToDto(item);
    }

    public async Task<PropertyRoomTypeDto> CreateAsync(int propertyId, CreatePropertyRoomTypeDto dto)
    {
        ValidateMonthlyRent(dto.MonthlyRent);
        await EnsurePropertyExistsAsync(propertyId);
        if (!await _repository.RoomTypeExistsAsync(dto.RoomTypeId))
        {
            throw new PropertyRoomTypeNotFoundException("Room type does not exist.");
        }
        if (await _repository.ExistsAsync(propertyId, dto.RoomTypeId))
        {
            throw new PropertyRoomTypeConflictException("This room type is already configured for the property.");
        }

        var entity = ConvertToEntity(propertyId, dto);
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();

        var created = await _repository.GetByIdAsync(propertyId, dto.RoomTypeId, _timeProvider.GetUtcNow());
        if (created is null)
        {
            throw new InvalidOperationException("Created property room type was not found.");
        }
        return ConvertToDto(created);
    }

    public async Task<PropertyRoomTypeDto?> UpdateAsync(
        int propertyId,
        int roomTypeId,
        UpdatePropertyRoomTypeDto dto)
    {
        ValidateMonthlyRent(dto.MonthlyRent);
        var entity = await _repository.GetTrackedByIdAsync(propertyId, roomTypeId);
        if (entity is null)
        {
            return null;
        }

        entity.MonthlyRent = dto.MonthlyRent;
        await _repository.SaveChangesAsync();
        return await GetByIdAsync(propertyId, roomTypeId, 0, RoleNames.Admin);
    }

    public async Task<PropertyRoomTypeDto?> UpdateStatusAsync(
        int propertyId,
        int roomTypeId,
        UpdatePropertyRoomTypeStatusDto dto)
    {
        var entity = await _repository.GetTrackedByIdAsync(propertyId, roomTypeId);
        if (entity is null)
        {
            return null;
        }

        entity.IsActive = dto.IsActive;
        await _repository.SaveChangesAsync();
        return await GetByIdAsync(propertyId, roomTypeId, 0, RoleNames.Admin);
    }

    private async Task EnsurePropertyExistsAsync(int propertyId)
    {
        if (!await _repository.PropertyExistsAsync(propertyId))
        {
            throw new PropertyRoomTypeNotFoundException("Property does not exist.");
        }
    }

    private async Task EnsureCanReadPropertyAsync(int propertyId, int requestingUserId, string role)
    {
        if (role == RoleNames.Admin)
        {
            return;
        }
        if (role != RoleNames.Manager ||
            !await _repository.HasActiveManagerAssignmentAsync(propertyId, requestingUserId))
        {
            throw new PropertyRoomTypeAuthorizationException(
                "Managers can only view room type configuration for properties with an active assignment.");
        }
    }

    private static void ValidateMonthlyRent(decimal monthlyRent)
    {
        if (monthlyRent <= 0m)
        {
            throw new PropertyRoomTypeValidationException("Monthly rent must be greater than zero.");
        }
    }

    private static PropertyRoomTypes ConvertToEntity(int propertyId, CreatePropertyRoomTypeDto dto) => new()
    {
        PropertyId = propertyId,
        RoomTypeId = dto.RoomTypeId,
        MonthlyRent = dto.MonthlyRent,
        IsActive = true
    };

    private static PropertyRoomTypeDto ConvertToDto(PropertyRoomTypeReadModel readModel) => new(
        readModel.PropertyId,
        readModel.RoomTypeId,
        readModel.RoomTypeName,
        readModel.MonthlyRent,
        readModel.IsActive,
        readModel.TotalRoomCount,
        readModel.AvailableRoomCount);
}

public sealed class PropertyRoomTypeNotFoundException(string message) : Exception(message);

public sealed class PropertyRoomTypeConflictException(string message) : Exception(message);

public sealed class PropertyRoomTypeValidationException(string message) : Exception(message);

public sealed class PropertyRoomTypeAuthorizationException(string message) : Exception(message);
