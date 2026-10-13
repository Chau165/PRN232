using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class RoomTypeService : IRoomTypeService
{
    private readonly IRoomTypeRepository _repository;
    private readonly IServiceRepository _serviceRepository;

    public RoomTypeService(IRoomTypeRepository repository, IServiceRepository serviceRepository)
    {
        _repository = repository;
        _serviceRepository = serviceRepository;
    }

    public Task<List<RoomTypeDto>> GetAllAsync() => _repository.GetAllAsync();

    public Task<RoomTypeDto?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

    public async Task<RoomTypeDto> CreateAsync(CreateRoomTypeDto dto)
    {
        Validate(dto.TypeName, dto.Price);

        var entity = await _repository.AddAsync(new RoomTypes { TypeName = dto.TypeName.Trim(), Price = dto.Price });
        await _repository.SaveChangesAsync();
        return new RoomTypeDto(entity.RoomTypeId, entity.TypeName, entity.Price);
    }

    public async Task<RoomTypeDto?> UpdateAsync(int id, UpdateRoomTypeDto dto)
    {
        var entity = await _repository.GetEntityByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        Validate(dto.TypeName, dto.Price);

        entity.TypeName = dto.TypeName.Trim();
        entity.Price = dto.Price;
        await _repository.SaveChangesAsync();
        return new RoomTypeDto(entity.RoomTypeId, entity.TypeName, entity.Price);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _repository.GetEntityByIdAsync(id);
        if (entity is null)
        {
            return false;
        }
        if (await _repository.HasRoomsAsync(id))
        {
            throw new RoomTypeHasRoomsException();
        }

        _repository.Remove(entity);
        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<List<ServiceDto>?> GetServicesAsync(int roomTypeId)
    {
        if (await _repository.GetEntityByIdAsync(roomTypeId) is null)
        {
            return null;
        }
        return await _repository.GetServicesAsync(roomTypeId);
    }

    public async Task<bool> AddServiceAsync(int roomTypeId, int serviceId)
    {
        if (await _repository.GetEntityByIdAsync(roomTypeId) is null)
        {
            throw new RoomTypeNotFoundException();
        }
        if (await _serviceRepository.GetEntityByIdAsync(serviceId) is null)
        {
            throw new ServiceNotFoundException();
        }
        if (await _repository.ServiceLinkExistsAsync(roomTypeId, serviceId))
        {
            return false;
        }

        await _repository.AddServiceLinkAsync(roomTypeId, serviceId);
        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveServiceAsync(int roomTypeId, int serviceId) =>
        await _repository.RemoveServiceLinkAsync(roomTypeId, serviceId) &&
        await SaveAndReturnTrueAsync();

    private async Task<bool> SaveAndReturnTrueAsync()
    {
        await _repository.SaveChangesAsync();
        return true;
    }

    private static void Validate(string typeName, decimal price)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new RoomTypeValidationException("Tên loại phòng không được để trống.");
        }
        if (price < 0)
        {
            throw new RoomTypeValidationException("Giá phòng không được âm.");
        }
    }
}

public class RoomTypeBusinessRuleException(string message) : Exception(message);

public sealed class RoomTypeValidationException(string message) : RoomTypeBusinessRuleException(message);

public sealed class RoomTypeHasRoomsException()
    : RoomTypeBusinessRuleException("Không thể xoá loại phòng còn phòng đang dùng.");

public sealed class RoomTypeNotFoundException() : RoomTypeBusinessRuleException("Loại phòng không tồn tại.");

public sealed class ServiceNotFoundException() : RoomTypeBusinessRuleException("Dịch vụ không tồn tại.");
