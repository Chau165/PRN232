using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class AmenityService : IAmenityService
{
    private readonly IAmenityRepository _repository;

    public AmenityService(IAmenityRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<AmenityDto>> GetAllAsync()
    {
        var amenities = await _repository.GetAllAsync();
        return amenities.Select(ConvertToDto).ToList();
    }

    public async Task<AmenityDto?> GetByIdAsync(int id)
    {
        var amenity = await _repository.GetByIdAsync(id);
        if (amenity is null)
        {
            return null;
        }
        return ConvertToDto(amenity);
    }

    public async Task<AmenityDto> CreateAsync(CreateAmenityDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ReferenceDataValidationException("Tên tiện ích không được để trống.");
        }

        var normalizedDto = dto with
        {
            Name = dto.Name.Trim()
        };
        var entity = ConvertToEntity(normalizedDto);
        entity = await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return ConvertToDto(entity);
    }

    public async Task<AmenityDto?> UpdateAsync(int id, UpdateAmenityDto dto)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ReferenceDataValidationException("Tên tiện ích không được để trống.");
        }

        var normalizedDto = dto with
        {
            Name = dto.Name.Trim()
        };
        ApplyUpdateToEntity(normalizedDto, entity);
        await _repository.SaveChangesAsync();
        return ConvertToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return false;
        }

        _repository.Remove(entity);
        await _repository.SaveChangesAsync();
        return true;
    }

    private static Amenities ConvertToEntity(CreateAmenityDto dto)
    {
        return new Amenities()
        {
            Name = dto.Name
        };
    }

    private static void ApplyUpdateToEntity(UpdateAmenityDto dto, Amenities entity)
    {
        entity.Name = dto.Name;
    }

    private static AmenityDto ConvertToDto(Amenities entity)
    {
        return new AmenityDto(entity.AmenityId, entity.Name);
    }
}

public class ReferenceDataValidationException(string message) : Exception(message);
