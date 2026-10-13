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

    public Task<List<ServiceDto>> GetAllAsync() => _repository.GetAllAsync();

    public Task<ServiceDto?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

    public async Task<ServiceDto> CreateAsync(CreateServiceDto dto)
    {
        Validate(dto.Name, dto.Price);

        var entity = await _repository.AddAsync(new Entities.Service { Name = dto.Name.Trim(), Price = dto.Price });
        await _repository.SaveChangesAsync();
        return new ServiceDto(entity.ServiceId, entity.Name, entity.Price);
    }

    public async Task<ServiceDto?> UpdateAsync(int id, UpdateServiceDto dto)
    {
        var entity = await _repository.GetEntityByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        Validate(dto.Name, dto.Price);

        entity.Name = dto.Name.Trim();
        entity.Price = dto.Price;
        await _repository.SaveChangesAsync();
        return new ServiceDto(entity.ServiceId, entity.Name, entity.Price);
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
}
