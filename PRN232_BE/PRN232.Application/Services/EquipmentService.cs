using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class EquipmentService : IEquipmentService
{
    private readonly IEquipmentRepository _repository;

    public EquipmentService(IEquipmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<EquipmentDto>> GetAllAsync(int? roomId)
    {
        var equipment = await _repository.GetAllAsync(roomId);
        return equipment.Select(ConvertToDto).ToList();
    }

    public async Task<EquipmentDto?> GetByIdAsync(int id)
    {
        var equipment = await _repository.GetByIdAsync(id);
        return equipment is null ? null : ConvertToDto(equipment);
    }

    public async Task<EquipmentDto> CreateAsync(CreateEquipmentDto dto, int requestingUserId, bool isAdmin)
    {
        if (!await _repository.RoomExistsAsync(dto.RoomId))
        {
            throw new EquipmentRoomNotFoundException();
        }
        await EnsureOwnerOrAdminAsync(dto.RoomId, requestingUserId, isAdmin);
        Validate(dto.Name, dto.Supplier, dto.Status);

        var entity = ConvertToEntity(dto);

        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return ConvertToDto(entity);
    }

    public async Task<EquipmentDto?> UpdateAsync(int id, UpdateEquipmentDto dto, int requestingUserId, bool isAdmin)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        await EnsureOwnerOrAdminAsync(entity.RoomId, requestingUserId, isAdmin);
        Validate(dto.Name, dto.Supplier, dto.Status);

        ApplyUpdateToEntity(dto, entity);

        await _repository.SaveChangesAsync();
        return ConvertToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id, int requestingUserId, bool isAdmin)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return false;
        }
        await EnsureOwnerOrAdminAsync(entity.RoomId, requestingUserId, isAdmin);

        _repository.Remove(entity);
        await _repository.SaveChangesAsync();
        return true;
    }

    private async Task EnsureOwnerOrAdminAsync(int roomId, int requestingUserId, bool isAdmin)
    {
        if (isAdmin)
        {
            return;
        }
        var ownerId = await _repository.GetOwnerIdAsync(roomId);
        if (ownerId != requestingUserId)
        {
            throw new EquipmentForbiddenException();
        }
    }

    private static void Validate(string name, string? supplier, string status)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new EquipmentValidationException("Tên thiết bị không được để trống.");
        }
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new EquipmentValidationException("Trạng thái thiết bị không được để trống.");
        }
    }

    private static Equipments ConvertToEntity(CreateEquipmentDto dto)
    {
        return new Equipments
        {
            RoomId = dto.RoomId,
            Name = dto.Name.Trim(),
            InstallDate = dto.InstallDate,
            WarrantyExpiry = dto.WarrantyExpiry,
            Supplier = dto.Supplier?.Trim() ?? string.Empty,
            Status = dto.Status.Trim()
        };
    }

    private static void ApplyUpdateToEntity(UpdateEquipmentDto dto, Equipments entity)
    {
        entity.Name = dto.Name.Trim();
        entity.InstallDate = dto.InstallDate;
        entity.WarrantyExpiry = dto.WarrantyExpiry;
        entity.Supplier = dto.Supplier?.Trim() ?? string.Empty;
        entity.Status = dto.Status.Trim();
    }

    private static EquipmentDto ConvertToDto(Equipments entity)
    {
        return new EquipmentDto(
            entity.EquipmentId,
            entity.RoomId,
            entity.Name,
            entity.InstallDate,
            entity.WarrantyExpiry,
            entity.Supplier,
            entity.Status);
    }
}

public class EquipmentBusinessRuleException(string message) : Exception(message);

public sealed class EquipmentValidationException(string message) : EquipmentBusinessRuleException(message);

public sealed class EquipmentRoomNotFoundException() : EquipmentBusinessRuleException("Phòng không tồn tại.");

public sealed class EquipmentForbiddenException()
    : EquipmentBusinessRuleException("Bạn không phải chủ sở hữu khu trọ chứa phòng này.");
