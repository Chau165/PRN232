using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class RoomService : IRoomService
{
    private readonly IRoomRepository _repository;

    public RoomService(IRoomRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<RoomDto>> GetAllAsync(int? propertyId, string? status, string? city, decimal? maxPrice)
    {
        var rooms = await _repository.GetAllAsync(propertyId, status, city, maxPrice);
        return rooms.Select(ConvertToDto).ToList();
    }

    public async Task<RoomDto?> GetByIdAsync(int id)
    {
        var room = await _repository.GetByIdAsync(id);
        if (room is null)
        {
            return null;
        }
        return ConvertToDto(room);
    }

    public async Task<List<RentableRoomDto>> GetRentableRoomsAsync(DateTimeOffset asOfUtc)
    {
        var rooms = await _repository.GetRentableRoomsAsync(asOfUtc);
        return rooms.Select(ConvertToDto).ToList();
    }

    public async Task<List<PropertyAvailabilityDto>> GetPropertyAvailabilityAsync(DateTimeOffset asOfUtc)
    {
        var properties = await _repository.GetPropertyAvailabilityAsync(asOfUtc);
        return properties.Select(ConvertToDto).ToList();
    }

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

        var status = RoomStatuses.Available;
        var room = ConvertToEntity(dto, status);

        var created = await _repository.AddAsync(room);
        await _repository.SaveChangesAsync();
        return ConvertToDto((await _repository.GetByIdAsync(created.RoomId))!);
    }

    public async Task<RoomDto?> UpdateAsync(int id, UpdateRoomDto dto, int requestingUserId, bool isAdmin)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
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

        ApplyUpdateToEntity(dto, entity);

        await _repository.SaveChangesAsync();
        var updated = await _repository.GetByIdAsync(id);
        if (updated is null)
        {
            return null;
        }
        return ConvertToDto(updated);
    }

    public async Task<bool> DeleteAsync(int id, int requestingUserId, bool isAdmin)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
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
        if (await _repository.GetTrackedByIdAsync(roomId) is null)
        {
            return null;
        }
        var images = await _repository.GetImagesAsync(roomId);
        return images.Select(ConvertToDto).ToList();
    }

    public async Task<RoomImageDto?> AddImageAsync(int roomId, AddRoomImageDto dto, int requestingUserId, bool isAdmin)
    {
        if (await _repository.GetTrackedByIdAsync(roomId) is null)
        {
            return null;
        }
        await EnsureOwnerOrAdminAsync(roomId, requestingUserId, isAdmin);
        if (string.IsNullOrWhiteSpace(dto.ImageUrl))
        {
            throw new RoomValidationException("URL ảnh không được để trống.");
        }

        var image = await _repository.AddImageAsync(roomId, ConvertToEntity(dto));
        await _repository.SaveChangesAsync();
        return ConvertToDto(image);
    }

    public async Task<bool> RemoveImageAsync(int roomId, int imageId, int requestingUserId, bool isAdmin)
    {
        if (await _repository.GetTrackedByIdAsync(roomId) is null)
        {
            return false;
        }
        await EnsureOwnerOrAdminAsync(roomId, requestingUserId, isAdmin);
        return await _repository.RemoveImageAsync(roomId, imageId);
    }

    public async Task<List<AmenityDto>?> GetAmenitiesAsync(int roomId)
    {
        if (await _repository.GetTrackedByIdAsync(roomId) is null)
        {
            return null;
        }
        var amenities = await _repository.GetAmenitiesAsync(roomId);
        return amenities.Select(ConvertToDto).ToList();
    }

    public async Task<bool> AddAmenityAsync(int roomId, int amenityId, int requestingUserId, bool isAdmin)
    {
        if (await _repository.GetTrackedByIdAsync(roomId) is null)
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
        if (await _repository.GetTrackedByIdAsync(roomId) is null)
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

    private static Rooms ConvertToEntity(CreateRoomDto dto, string status)
    {
        return new Rooms
        {
            PropertyId = dto.PropertyId,
            RoomTypeId = dto.RoomTypeId,
            RoomCode = dto.RoomCode.Trim(),
            AreaSqm = dto.AreaSqm,
            Status = status
        };
    }

    private static void ApplyUpdateToEntity(UpdateRoomDto dto, Rooms entity)
    {
        entity.RoomTypeId = dto.RoomTypeId;
        entity.RoomCode = dto.RoomCode.Trim();
        entity.AreaSqm = dto.AreaSqm;
        entity.Status = dto.Status;
    }

    private static RoomImages ConvertToEntity(AddRoomImageDto dto)
    {
        return new RoomImages
        {
            ImageUrl = dto.ImageUrl.Trim(),
            IsPrimary = dto.IsPrimary
        };
    }

    private static RoomImageDto ConvertToDto(RoomImages entity)
    {
        return new RoomImageDto(
            entity.RoomImageId,
            entity.RoomId,
            entity.ImageUrl,
            entity.IsPrimary);
    }

    private static RoomImageDto ConvertToDto(RoomImageReadModel readModel)
    {
        return new RoomImageDto(
            readModel.RoomImageId,
            readModel.RoomId,
            readModel.ImageUrl,
            readModel.IsPrimary);
    }

    private static RoomDto ConvertToDto(RoomSummaryReadModel readModel)
    {
        return new RoomDto(
            readModel.RoomId,
            readModel.PropertyId,
            readModel.PropertyName,
            readModel.RoomTypeId,
            readModel.RoomTypeName,
            readModel.RoomTypePrice,
            readModel.RoomCode,
            readModel.AreaSqm,
            readModel.Status,
            readModel.PrimaryImageUrl);
    }

    private static RentableRoomDto ConvertToDto(RentableRoomReadModel readModel)
    {
        return new RentableRoomDto(
            readModel.RoomId,
            readModel.PropertyId,
            readModel.RoomTypeId,
            readModel.RoomTypeName,
            readModel.Price,
            readModel.RoomCode,
            readModel.AreaSqm);
    }

    private static PropertyAvailabilityDto ConvertToDto(PropertyAvailabilityReadModel readModel)
    {
        return new PropertyAvailabilityDto(
            readModel.PropertyId,
            readModel.AvailableRoomCount,
            readModel.StartingPrice);
    }

    private static AmenityDto ConvertToDto(Amenities entity)
    {
        return new AmenityDto(entity.AmenityId, entity.Name);
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
