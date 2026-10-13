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

    public Task<List<AmenityDto>> GetAllAsync() => _repository.GetAllAsync();

    public Task<AmenityDto?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

    public async Task<AmenityDto> CreateAsync(CreateAmenityDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ReferenceDataValidationException("Tên tiện ích không được để trống.");
        }

        var entity = await _repository.AddAsync(new Amenities { Name = dto.Name.Trim() });
        await _repository.SaveChangesAsync();
        return new AmenityDto(entity.AmenityId, entity.Name);
    }

    public async Task<AmenityDto?> UpdateAsync(int id, UpdateAmenityDto dto)
    {
        var entity = await _repository.GetEntityByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ReferenceDataValidationException("Tên tiện ích không được để trống.");
        }

        entity.Name = dto.Name.Trim();
        await _repository.SaveChangesAsync();
        return new AmenityDto(entity.AmenityId, entity.Name);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _repository.GetEntityByIdAsync(id);
        if (entity is null)
        {
            return false;
        }

        _repository.Remove(entity);
        await _repository.SaveChangesAsync();
        return true;
    }
}

public class ReferenceDataValidationException(string message) : Exception(message);
