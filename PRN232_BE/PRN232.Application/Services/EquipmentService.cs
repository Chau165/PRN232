using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class EquipmentService : IEquipmentService
{
    private const int DefaultPageSize = 50;
    private const int MaximumPageSize = 50;
    private readonly IEquipmentRepository _repository;

    public EquipmentService(IEquipmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<EquipmentDto>> GetAllAsync(
        int? roomId,
        int? propertyId,
        string? status,
        string? search,
        int requestingUserId,
        string role,
        int? page = null,
        int? pageSize = null)
    {
        EnsureManagementRole(role);
        int? managerId = role == RoleNames.Admin ? null : requestingUserId;
        var paging = NormalizePaging(page, pageSize);
        var equipment = await _repository.GetAllAsync(
            roomId,
            propertyId,
            status,
            search,
            managerId,
            paging?.Page,
            paging?.PageSize);
        return equipment.Select(ConvertToDto).ToList();
    }

    public async Task<EquipmentDto?> GetByIdAsync(int id, int requestingUserId, string role)
    {
        EnsureManagementRole(role);
        int? managerId = role == RoleNames.Admin ? null : requestingUserId;
        var equipment = await _repository.GetByIdAsync(id, managerId);
        if (equipment is null)
        {
            return null;
        }
        return ConvertToDto(equipment);
    }

    public async Task<EquipmentDto> CreateAsync(CreateEquipmentDto dto, int requestingUserId, string role)
    {
        EnsureManagementRole(role);
        if (!await _repository.RoomExistsAsync(dto.RoomId))
        {
            throw new EquipmentRoomNotFoundException();
        }
        await EnsureRoomAccessAsync(dto.RoomId, requestingUserId, role);
        Validate(dto);

        var entity = ConvertToEntity(dto);

        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        var created = await _repository.GetByIdAsync(
            entity.EquipmentId,
            role == RoleNames.Admin ? null : requestingUserId);
        if (created is null)
        {
            throw new InvalidOperationException("Created equipment was not found.");
        }
        return ConvertToDto(created);
    }

    public async Task<EquipmentDto?> UpdateAsync(int id, UpdateEquipmentDto dto, int requestingUserId, string role)
    {
        EnsureManagementRole(role);
        var entity = await _repository.GetTrackedByIdAsync(
            id,
            role == RoleNames.Admin ? null : requestingUserId);
        if (entity is null)
        {
            return null;
        }
        await EnsureRoomAccessAsync(entity.RoomId, requestingUserId, role);
        Validate(dto);

        ApplyUpdateToEntity(dto, entity);

        await _repository.SaveChangesAsync();
        var updated = await _repository.GetByIdAsync(
            entity.EquipmentId,
            role == RoleNames.Admin ? null : requestingUserId);
        if (updated is null)
        {
            throw new InvalidOperationException("Updated equipment was not found.");
        }
        return ConvertToDto(updated);
    }

    public async Task<bool> DeleteAsync(int id, int requestingUserId, string role)
    {
        EnsureManagementRole(role);
        var entity = await _repository.GetTrackedByIdAsync(
            id,
            role == RoleNames.Admin ? null : requestingUserId);
        if (entity is null)
        {
            return false;
        }
        await EnsureRoomAccessAsync(entity.RoomId, requestingUserId, role);
        if (await _repository.HasMaintenanceDependenciesAsync(id))
        {
            throw new EquipmentDependencyConflictException();
        }

        _repository.Remove(entity);
        await _repository.SaveChangesAsync();
        return true;
    }

    private async Task EnsureRoomAccessAsync(int roomId, int requestingUserId, string role)
    {
        if (role == RoleNames.Admin)
        {
            return;
        }
        if (!await _repository.HasRoomAccessAsync(roomId, requestingUserId))
        {
            throw new EquipmentForbiddenException();
        }
    }

    private static void Validate(CreateEquipmentDto dto)
    {
        Validate(dto.Name, dto.Supplier, dto.Status, dto.InstallDate, dto.WarrantyExpiry);
    }

    private static void Validate(UpdateEquipmentDto dto)
    {
        Validate(dto.Name, dto.Supplier, dto.Status, dto.InstallDate, dto.WarrantyExpiry);
    }

    private static void Validate(
        string name,
        string? supplier,
        string status,
        DateOnly installDate,
        DateOnly warrantyExpiry)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new EquipmentValidationException("Tên thiết bị là bắt buộc và không quá 200 ký tự.");
        }
        if (string.IsNullOrWhiteSpace(supplier) || supplier.Trim().Length > 200)
        {
            throw new EquipmentValidationException("Nhà cung cấp là bắt buộc và không quá 200 ký tự.");
        }
        if (warrantyExpiry < installDate)
        {
            throw new EquipmentValidationException("Hạn bảo hành không được trước ngày lắp đặt.");
        }
        if (!EquipmentStatuses.IsValid(status.Trim()))
        {
            throw new EquipmentValidationException("Trạng thái thiết bị không được hỗ trợ.");
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

    private static EquipmentDto ConvertToDto(EquipmentReadModel equipment)
    {
        return new EquipmentDto(
            equipment.EquipmentId,
            equipment.RoomId,
            equipment.Name,
            equipment.InstallDate,
            equipment.WarrantyExpiry,
            equipment.Supplier,
            equipment.Status,
            equipment.RoomCode,
            equipment.PropertyId,
            equipment.PropertyName);
    }

    private static void EnsureManagementRole(string role)
    {
        if (role is not (RoleNames.Admin or RoleNames.Manager))
        {
            throw new EquipmentForbiddenException();
        }
    }

    private static (int Page, int PageSize)? NormalizePaging(int? page, int? pageSize)
    {
        if (!page.HasValue && !pageSize.HasValue)
        {
            return null;
        }

        var normalizedPage = page ?? 1;
        var normalizedPageSize = pageSize ?? DefaultPageSize;
        if (normalizedPage < 1)
        {
            throw new EquipmentValidationException("Trang phải lớn hơn hoặc bằng 1.");
        }
        if (normalizedPageSize is < 1 or > MaximumPageSize)
        {
            throw new EquipmentValidationException(
                $"Kích thước trang phải từ 1 đến {MaximumPageSize}.");
        }
        if ((long)(normalizedPage - 1) * normalizedPageSize > int.MaxValue)
        {
            throw new EquipmentValidationException("Trang yêu cầu vượt quá giới hạn hỗ trợ.");
        }

        return (normalizedPage, normalizedPageSize);
    }
}

public class EquipmentBusinessRuleException(string message) : Exception(message);

public sealed class EquipmentValidationException(string message) : EquipmentBusinessRuleException(message);

public sealed class EquipmentRoomNotFoundException() : EquipmentBusinessRuleException("Phòng không tồn tại.");

public sealed class EquipmentForbiddenException()
    : EquipmentBusinessRuleException("Bạn chưa được phân công quản lý khu trọ chứa phòng này.");

public sealed class EquipmentDependencyConflictException()
    : EquipmentBusinessRuleException("Không thể xóa thiết bị đã có lịch sử bảo trì liên quan.");
