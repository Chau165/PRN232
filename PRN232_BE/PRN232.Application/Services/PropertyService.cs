using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class PropertyService : IPropertyService
{
    private readonly IPropertyRepository _repository;

    public PropertyService(IPropertyRepository repository)
    {
        _repository = repository;
    }

    public Task<List<PropertyDto>> GetAllAsync(int? ownerId) => _repository.GetAllAsync(ownerId);

    public Task<PropertyDto?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

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

        var property = new Property
        {
            OwnerId = ownerId,
            PropertyName = dto.PropertyName.Trim(),
            AddressText = dto.AddressText.Trim(),
            Ward = dto.Ward?.Trim() ?? string.Empty,
            District = dto.District?.Trim() ?? string.Empty,
            City = dto.City?.Trim() ?? string.Empty,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
        };

        var created = await _repository.AddAsync(property);
        await _repository.SaveChangesAsync();

        // re-fetch qua GetByIdAsync để có OwnerName + RoomCount (projection), thay vì
        // tự ráp DTO tay thiếu field.
        return (await _repository.GetByIdAsync(created.PropertyId))!;
    }

    public async Task<PropertyDto?> UpdateAsync(int id, UpdatePropertyDto dto, int requestingUserId, bool isAdmin)
    {
        var entity = await _repository.GetEntityByIdAsync(id);
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

        entity.PropertyName = dto.PropertyName.Trim();
        entity.AddressText = dto.AddressText.Trim();
        entity.Ward = dto.Ward?.Trim() ?? string.Empty;
        entity.District = dto.District?.Trim() ?? string.Empty;
        entity.City = dto.City?.Trim() ?? string.Empty;
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;

        await _repository.SaveChangesAsync();
        return await _repository.GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id, int requestingUserId, bool isAdmin)
    {
        var entity = await _repository.GetEntityByIdAsync(id);
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
}

public class PropertyBusinessRuleException(string message) : Exception(message);

public sealed class PropertyValidationException(string message) : PropertyBusinessRuleException(message);

public sealed class PropertyForbiddenException()
    : PropertyBusinessRuleException("Bạn không phải chủ sở hữu của khu trọ này.");

public sealed class PropertyHasRoomsException()
    : PropertyBusinessRuleException("Không thể xoá khu trọ còn phòng — xoá hết phòng trước.");
