using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;

namespace PRN232.Application.Services;

public class PropertyService : IPropertyService
{
    private readonly IPropertyRepository _repository;

    public PropertyService(IPropertyRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PropertyDto>> GetAllAsync(int? ownerId)
    {
        var properties = await _repository.GetAllAsync(ownerId);
        return properties.Select(ConvertToDto).ToList();
    }

    public async Task<PropertyDto?> GetByIdAsync(int id)
    {
        var property = await _repository.GetByIdAsync(id);
        if (property is null)
        {
            return null;
        }
        return ConvertToDto(property);
    }

    public async Task<PropertyDto> CreateAsync(CreatePropertyDto dto, int ownerId)
    {
        if (string.IsNullOrWhiteSpace(dto.PropertyName))
        {
            throw new PropertyValidationException("Tên khu trọ không được để trống.");
        }
        if (string.IsNullOrWhiteSpace(dto.AddressText))
        {
            throw new PropertyValidationException("Địa chỉ không được để trống.");
        }

        var property = ConvertToEntity(dto, ownerId);

        var created = await _repository.AddAsync(property);
        await _repository.SaveChangesAsync();

        return ConvertToDto(await GetPropertyResponseAsync(created.PropertyId));
    }

    public async Task<PropertyDto?> UpdateAsync(int id, UpdatePropertyDto dto, int requestingUserId, bool isAdmin)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        if (!isAdmin && entity.OwnerId != requestingUserId)
        {
            throw new PropertyForbiddenException();
        }
        if (string.IsNullOrWhiteSpace(dto.PropertyName))
        {
            throw new PropertyValidationException("Tên khu trọ không được để trống.");
        }

        ApplyUpdateToEntity(dto, entity);

        await _repository.SaveChangesAsync();
        var updated = await _repository.GetByIdAsync(id);
        if (updated is null)
        {
            return null;
        }
        return ConvertToDto(updated);
    }

    public async Task<bool> DeleteAsync(int id, int requestingUserId, bool isAdmin)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return false;
        }
        if (!isAdmin && entity.OwnerId != requestingUserId)
        {
            throw new PropertyForbiddenException();
        }
        if (await _repository.HasRoomsAsync(id))
        {
            throw new PropertyHasRoomsException();
        }

        _repository.Remove(entity);
        await _repository.SaveChangesAsync();
        return true;
    }

    private static PRN232.Domain.Entities.Property ConvertToEntity(CreatePropertyDto dto, int ownerId)
    {
        return new PRN232.Domain.Entities.Property
        {
            OwnerId = ownerId,
            PropertyName = dto.PropertyName.Trim(),
            AddressText = dto.AddressText.Trim(),
            Ward = dto.Ward?.Trim() ?? string.Empty,
            District = dto.District?.Trim() ?? string.Empty,
            City = dto.City?.Trim() ?? string.Empty,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };
    }

    private static void ApplyUpdateToEntity(UpdatePropertyDto dto, PRN232.Domain.Entities.Property entity)
    {
        entity.PropertyName = dto.PropertyName.Trim();
        entity.AddressText = dto.AddressText.Trim();
        entity.Ward = dto.Ward?.Trim() ?? string.Empty;
        entity.District = dto.District?.Trim() ?? string.Empty;
        entity.City = dto.City?.Trim() ?? string.Empty;
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;
    }

    private static PropertyDto ConvertToDto(PropertySummaryReadModel readModel)
    {
        return new PropertyDto(
            readModel.PropertyId,
            readModel.OwnerId,
            readModel.OwnerName,
            readModel.PropertyName,
            readModel.AddressText,
            readModel.Ward,
            readModel.District,
            readModel.City,
            readModel.Latitude,
            readModel.Longitude,
            readModel.RoomCount);
    }

    private async Task<PropertySummaryReadModel> GetPropertyResponseAsync(int propertyId)
    {
        var property = await _repository.GetByIdAsync(propertyId);
        if (property is null)
        {
            throw new InvalidOperationException("Created property was not found.");
        }
        return property;
    }
}

public class PropertyBusinessRuleException(string message) : Exception(message);

public sealed class PropertyValidationException(string message) : PropertyBusinessRuleException(message);

public sealed class PropertyForbiddenException()
    : PropertyBusinessRuleException("Bạn không phải chủ sở hữu của khu trọ này.");

public sealed class PropertyHasRoomsException()
    : PropertyBusinessRuleException("Không thể xoá khu trọ còn phòng — xoá hết phòng trước.");
