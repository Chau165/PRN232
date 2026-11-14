using System.Text;
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

    public async Task<List<RoomTypeDto>> GetAllAsync()
    {
        var roomTypes = await _repository.GetAllAsync();
        return roomTypes.Select(ConvertToDto).ToList();
    }

    public async Task<RoomTypeDto?> GetByIdAsync(int id)
    {
        var roomType = await _repository.GetByIdAsync(id);
        if (roomType is null)
        {
            return null;
        }
        return ConvertToDto(roomType);
    }

    public async Task<RoomTypeDto> CreateAsync(CreateRoomTypeDto dto)
    {
        var normalizedTypeName = NormalizeTypeName(dto.TypeName);
        Validate(normalizedTypeName, dto.Price);

        if (await _repository.ExistsByNameAsync(normalizedTypeName))
        {
            throw new RoomTypeConflictException(
                $"Loại phòng \"{normalizedTypeName}\" đã tồn tại trong danh mục.");
        }

        var entity = ConvertToEntity(normalizedTypeName, dto.Price);
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return ConvertToDto(entity);
    }

    public async Task<RoomTypeDto?> UpdateAsync(int id, UpdateRoomTypeDto dto)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        Validate(dto.TypeName, dto.Price);

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
        if (await _repository.GetTrackedByIdAsync(roomTypeId) is null)
        {
            return null;
        }
        var services = await _repository.GetServicesAsync(roomTypeId);
        return services.Select(ConvertToDto).ToList();
    }

    public async Task<bool> AddServiceAsync(int roomTypeId, int serviceId)
    {
        if (await _repository.GetTrackedByIdAsync(roomTypeId) is null)
        {
            throw new RoomTypeNotFoundException();
        }
        if (await _serviceRepository.GetTrackedByIdAsync(serviceId) is null)
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

    public async Task<bool> RemoveServiceAsync(int roomTypeId, int serviceId)
    {
        return await _repository.RemoveServiceLinkAsync(roomTypeId, serviceId) &&
            await SaveAndReturnTrueAsync();
    }

    private async Task<bool> SaveAndReturnTrueAsync()
    {
        await _repository.SaveChangesAsync();
        return true;
    }

    private static string NormalizeTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        var normalized = typeName.Normalize(NormalizationForm.FormC);
        var parts = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
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

    private static RoomTypes ConvertToEntity(string typeName, decimal price)
    {
        return new RoomTypes
        {
            TypeName = typeName,
            Price = price
        };
    }

    private static void ApplyUpdateToEntity(UpdateRoomTypeDto dto, RoomTypes entity)
    {
        entity.TypeName = dto.TypeName.Trim();
        entity.Price = dto.Price;
    }

    private static RoomTypeDto ConvertToDto(RoomTypes entity)
    {
        return new RoomTypeDto(entity.RoomTypeId, entity.TypeName, entity.Price);
    }

    private static ServiceDto ConvertToDto(Service entity)
    {
        return new ServiceDto(entity.ServiceId, entity.Name, entity.Price);
    }
}

public class RoomTypeBusinessRuleException(string message) : Exception(message);

public sealed class RoomTypeValidationException(string message) : RoomTypeBusinessRuleException(message);

public sealed class RoomTypeConflictException(string message) : RoomTypeBusinessRuleException(message);

public sealed class RoomTypeHasRoomsException()
    : RoomTypeBusinessRuleException("Không thể xoá loại phòng còn phòng đang dùng.");

public sealed class RoomTypeNotFoundException() : RoomTypeBusinessRuleException("Loại phòng không tồn tại.");

public sealed class ServiceNotFoundException() : RoomTypeBusinessRuleException("Dịch vụ không tồn tại.");
