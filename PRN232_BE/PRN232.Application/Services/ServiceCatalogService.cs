using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using Entities = PRN232.Domain.Entities;

namespace PRN232.Application.Services;

// Tên "ServiceCatalogService" (không phải "ServiceService") để khỏi trùng/đọc gây
// nhầm với khái niệm "Service" (dịch vụ thuê phòng: điện, nước, wifi...) mà entity
// này đại diện.
public class ServiceCatalogService : IServiceService
{
    private readonly IServiceRepository _repository;

    public ServiceCatalogService(IServiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ServiceDto>> GetAllAsync()
    {
        var entities = await _repository.GetAllAsync();
        return entities.Select(ConvertToDto).ToList();
    }

    public async Task<ServiceDto?> GetByIdAsync(int id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        return ConvertToDto(entity);
    }

    public async Task<ServiceDto> CreateAsync(CreateServiceDto dto)
    {
        Validate(dto.Name, dto.Price);

        var entity = ConvertToEntity(dto);
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return ConvertToDto(entity);
    }

    public async Task<ServiceDto?> UpdateAsync(int id, UpdateServiceDto dto)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        Validate(dto.Name, dto.Price);

        ApplyUpdateToEntity(dto, entity);
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

    private static void Validate(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ReferenceDataValidationException("Tên dịch vụ không được để trống.");
        }
        if (price < 0)
        {
            throw new ReferenceDataValidationException("Giá dịch vụ không được âm.");
        }
    }

    private static Entities.Service ConvertToEntity(CreateServiceDto dto)
    {
        return new Entities.Service
        {
            Name = dto.Name.Trim(),
            Price = dto.Price
        };
    }

    private static void ApplyUpdateToEntity(UpdateServiceDto dto, Entities.Service entity)
    {
        entity.Name = dto.Name.Trim();
        entity.Price = dto.Price;
    }

    private static ServiceDto ConvertToDto(Entities.Service entity)
    {
        return new ServiceDto(entity.ServiceId, entity.Name, entity.Price);
    }
}
