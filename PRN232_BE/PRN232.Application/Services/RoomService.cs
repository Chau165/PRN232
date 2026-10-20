using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class RoomService : IRoomService
{
    private readonly IRoomRepository _repository;

    public RoomService(IRoomRepository repository)
    {
        _repository = repository;
    }

    public Task<List<RoomDto>> GetAllAsync(int? propertyId, string? status, string? city, decimal? maxPrice) =>
        _repository.GetAllAsync(propertyId, status, city, maxPrice);

    public Task<RoomDto?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

    public Task<List<RentableRoomDto>> GetRentableRoomsAsync(DateTimeOffset asOfUtc) =>
        _repository.GetRentableRoomsAsync(asOfUtc);

    public Task<List<PropertyAvailabilityDto>> GetPropertyAvailabilityAsync(DateTimeOffset asOfUtc) =>
        _repository.GetPropertyAvailabilityAsync(asOfUtc);

    public async Task<RoomDto> CreateAsync(CreateRoomDto dto, int requestingUserId, bool isAdmin)
    {
        if (!await _repository.PropertyExistsAsync(dto.PropertyId))
        {
            throw new RoomPropertyNotFoundException();
        }
        if (!isAdmin)
        {
            var ownerId = await _repository.GetPropertyOwnerIdAsync(dto.PropertyId);
            if (ownerId != requestingUserId)
            {
                throw new RoomForbiddenException();
            }
        }
        if (!await _repository.RoomTypeExistsAsync(dto.RoomTypeId))
        {
            throw new RoomTypeNotFoundException();
        }
        if (string.IsNullOrWhiteSpace(dto.RoomCode))
        {
            throw new RoomValidationException("Mã phòng không được để trống.");
        }
        if (dto.AreaSqm <= 0)
        {
            throw new RoomValidationException("Diện tích phòng phải lớn hơn 0.");
        }

        var room = new Rooms
        {
            PropertyId = dto.PropertyId,
            RoomTypeId = dto.RoomTypeId,
            RoomCode = dto.RoomCode.Trim(),
            AreaSqm = dto.AreaSqm,
            Status = RoomStatuses.Available,
        };

        var created = await _repository.AddAsync(room);
        await _repository.SaveChangesAsync();
        return (await _repository.GetByIdAsync(created.RoomId))!;
    }

    public async Task<RoomDto?> UpdateAsync(int id, UpdateRoomDto dto, int requestingUserId, bool isAdmin)
    {
        var entity = await _repository.GetEntityByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        await EnsureOwnerOrAdminAsync(entity.RoomId, requestingUserId, isAdmin);

        if (!await _repository.RoomTypeExistsAsync(dto.RoomTypeId))
        {
            throw new RoomTypeNotFoundException();
        }
        if (string.IsNullOrWhiteSpace(dto.RoomCode))
        {
            throw new RoomValidationException("Mã phòng không được để trống.");
        }
        if (dto.AreaSqm <= 0)
        {
            throw new RoomValidationException("Diện tích phòng phải lớn hơn 0.");
        }
        if (!RoomStatuses.All.Contains(dto.Status))
        {
            throw new RoomValidationException(
                $"Trạng thái không hợp lệ — chỉ nhận: {string.Join(", ", RoomStatuses.All)}.");
        }

        entity.RoomTypeId = dto.RoomTypeId;
        entity.RoomCode = dto.RoomCode.Trim();
        entity.AreaSqm = dto.AreaSqm;
        entity.Status = dto.Status;

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
        await EnsureOwnerOrAdminAsync(entity.RoomId, requestingUserId, isAdmin);

        if (await _repository.HasDependentsAsync(id))
        {
            throw new RoomHasDependentsException();
        }

        _repository.Remove(entity);
        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<List<RoomImageDto>?> GetImagesAsync(int roomId)
    {
        if (await _repository.GetEntityByIdAsync(roomId) is null)
        {
            return null;
        }
        return await _repository.GetImagesAsync(roomId);
    }

    public async Task<RoomImageDto?> AddImageAsync(int roomId, AddRoomImageDto dto, int requestingUserId, bool isAdmin)
    {
        if (await _repository.GetEntityByIdAsync(roomId) is null)
        {
            return null;
        }
        await EnsureOwnerOrAdminAsync(roomId, requestingUserId, isAdmin);
        if (string.IsNullOrWhiteSpace(dto.ImageUrl))
        {
            throw new RoomValidationException("URL ảnh không được để trống.");
        }

        var image = await _repository.AddImageAsync(roomId, new RoomImages { ImageUrl = dto.ImageUrl.Trim(), IsPrimary = dto.IsPrimary });
        await _repository.SaveChangesAsync();
        return new RoomImageDto(image.RoomImageId, image.RoomId, image.ImageUrl, image.IsPrimary);
    }

    public async Task<bool> RemoveImageAsync(int roomId, int imageId, int requestingUserId, bool isAdmin)
    {
        if (await _repository.GetEntityByIdAsync(roomId) is null)
        {
            return false;
        }
        await EnsureOwnerOrAdminAsync(roomId, requestingUserId, isAdmin);
        return await _repository.RemoveImageAsync(roomId, imageId);
    }

    public async Task<List<AmenityDto>?> GetAmenitiesAsync(int roomId)
    {
        if (await _repository.GetEntityByIdAsync(roomId) is null)
        {
            return null;
        }
        return await _repository.GetAmenitiesAsync(roomId);
    }

    public async Task<bool> AddAmenityAsync(int roomId, int amenityId, int requestingUserId, bool isAdmin)
    {
        if (await _repository.GetEntityByIdAsync(roomId) is null)
        {
            throw new RoomNotFoundException();
        }
        if (!await _repository.AmenityExistsAsync(amenityId))
        {
            throw new RoomAmenityNotFoundException();
        }
        await EnsureOwnerOrAdminAsync(roomId, requestingUserId, isAdmin);

        if (await _repository.AmenityLinkExistsAsync(roomId, amenityId))
        {
            return false;
        }
        await _repository.AddAmenityLinkAsync(roomId, amenityId);
        return true;
    }

    public async Task<bool> RemoveAmenityAsync(int roomId, int amenityId, int requestingUserId, bool isAdmin)
    {
        if (await _repository.GetEntityByIdAsync(roomId) is null)
        {
            return false;
        }
        await EnsureOwnerOrAdminAsync(roomId, requestingUserId, isAdmin);
        return await _repository.RemoveAmenityLinkAsync(roomId, amenityId);
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
            throw new RoomForbiddenException();
        }
    }
}

public class RoomBusinessRuleException(string message) : Exception(message);

public sealed class RoomValidationException(string message) : RoomBusinessRuleException(message);

public sealed class RoomPropertyNotFoundException() : RoomBusinessRuleException("Khu trọ không tồn tại.");

public sealed class RoomNotFoundException() : RoomBusinessRuleException("Phòng không tồn tại.");

public sealed class RoomAmenityNotFoundException() : RoomBusinessRuleException("Tiện ích không tồn tại.");

public sealed class RoomForbiddenException()
    : RoomBusinessRuleException("Bạn không phải chủ sở hữu khu trọ chứa phòng này.");

public sealed class RoomHasDependentsException()
    : RoomBusinessRuleException("Không thể xoá phòng đang có thiết bị/hợp đồng/booking gắn vào.");
