using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class RoomService : IRoomService
{
    private readonly IRoomRepository _repository;
    private readonly IRoomImageStorage? _imageStorage;

    public RoomService(IRoomRepository repository, IRoomImageStorage? imageStorage = null)
    {
        _repository = repository;
        _imageStorage = imageStorage;
    }

    public async Task<List<RoomDto>> GetAllAsync(
        int? propertyId,
        string? status,
        string? city,
        decimal? maxPrice,
        int? managerId = null,
        int? roomTypeId = null,
        string? search = null)
    {
        var rooms = await _repository.GetAllAsync(
            propertyId,
            status,
            city,
            maxPrice,
            managerId,
            roomTypeId,
            search);
        return rooms.Select(ConvertToDto).ToList();
    }

    public async Task<RoomDto?> GetByIdAsync(int id, int? managerId = null)
    {
        var room = await _repository.GetByIdAsync(id, managerId);
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
        if (!isAdmin)
        {
            throw new RoomForbiddenException();
        }
        if (!await _repository.PropertyExistsAsync(dto.PropertyId))
        {
            throw new RoomPropertyNotFoundException();
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
        if (await _repository.RoomCodeExistsAsync(dto.PropertyId, dto.RoomCode))
        {
            throw new RoomValidationException("Mã phòng đã tồn tại trong khu trọ.");
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
        if (!isAdmin)
        {
            throw new RoomForbiddenException();
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
        if (await _repository.RoomCodeExistsAsync(entity.PropertyId, dto.RoomCode, id))
        {
            throw new RoomValidationException("Mã phòng đã tồn tại trong khu trọ.");
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
        if (!isAdmin)
        {
            throw new RoomForbiddenException();
        }

        if (entity.Status is RoomStatuses.Occupied or RoomStatuses.Reserved ||
            await _repository.HasDependentsAsync(id))
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
        await EnsureImageAccessAsync(roomId, requestingUserId, isAdmin);
        if (string.IsNullOrWhiteSpace(dto.ImageUrl))
        {
            throw new RoomValidationException("URL ảnh không được để trống.");
        }

        var image = await _repository.AddImageAsync(roomId, ConvertToEntity(dto));
        await _repository.SaveChangesAsync();
        return ConvertToDto(image);
    }

    public async Task<RoomImageDto?> UploadImageAsync(
        int roomId, RoomImageInput image, bool isPrimary, int requestingUserId, bool isAdmin)
    {
        if (await _repository.GetTrackedByIdAsync(roomId) is null)
        {
            return null;
        }
        await EnsureImageAccessAsync(roomId, requestingUserId, isAdmin);
        ValidateImage(image);
        if (_imageStorage is null)
        {
            throw new RoomImageStorageException();
        }

        string imageUrl;
        try
        {
            imageUrl = await _imageStorage.UploadAsync(image);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new RoomImageStorageException();
        }

        try
        {
            var entity = await _repository.AddImageAsync(roomId, new RoomImages
            {
                ImageUrl = imageUrl,
                IsPrimary = isPrimary
            });
            await _repository.SaveChangesAsync();
            return ConvertToDto(entity);
        }
        catch
        {
            // Preserve the database error if compensating storage cleanup fails.
            try { await _imageStorage.DeleteAsync(imageUrl); }
            catch { }
            throw;
        }
    }

    public async Task<bool> SetPrimaryImageAsync(int roomId, int imageId, int requestingUserId, bool isAdmin)
    {
        if (await _repository.GetTrackedByIdAsync(roomId) is null)
        {
            return false;
        }
        await EnsureImageAccessAsync(roomId, requestingUserId, isAdmin);
        return await _repository.SetPrimaryImageAsync(roomId, imageId);
    }

    public async Task<bool> RemoveImageAsync(int roomId, int imageId, int requestingUserId, bool isAdmin)
    {
        if (await _repository.GetTrackedByIdAsync(roomId) is null)
        {
            return false;
        }
        await EnsureImageAccessAsync(roomId, requestingUserId, isAdmin);
        var image = (await _repository.GetImagesAsync(roomId)).FirstOrDefault(i => i.RoomImageId == imageId);
        if (image is null)
        {
            return false;
        }
        // Delete the object first: on a storage failure the record remains available for retry.
        if (_imageStorage is not null && !await _repository.IsImageUrlInUseAsync(image.ImageUrl, imageId))
        {
            try { await _imageStorage.DeleteAsync(image.ImageUrl); }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                throw new RoomImageStorageException();
            }
        }
        return await _repository.RemoveImageAsync(roomId, imageId);
    }

    private async Task EnsureImageAccessAsync(int roomId, int requestingUserId, bool isAdmin)
    {
        if (!isAdmin && await _repository.GetByIdAsync(roomId, requestingUserId) is null)
        {
            throw new RoomForbiddenException();
        }
    }

    private static void ValidateImage(RoomImageInput image)
    {
        if (image.Content.Length is 0 or > 5 * 1024 * 1024)
        {
            throw new RoomValidationException("Ảnh phải có dung lượng từ 1 byte đến 5 MiB.");
        }
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var content = image.Content.AsSpan();
        var isJpeg = image.ContentType == "image/jpeg" &&
                     (extension is ".jpg" or ".jpeg") && content.Length >= 4 &&
                     content.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) &&
                     content[^2..].SequenceEqual(new byte[] { 0xFF, 0xD9 });
        var isPng = image.ContentType == "image/png" && extension == ".png" && content.Length >= 20 &&
                    content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) &&
                    content[^8..].SequenceEqual(new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 });
        var isWebp = image.ContentType == "image/webp" && extension == ".webp" && content.Length >= 20 &&
                     content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8) &&
                     System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(content[4..8]) == content.Length - 8;
        if (!isJpeg && !isPng && !isWebp)
        {
            throw new RoomValidationException("Ảnh phải là tệp JPEG, PNG hoặc WebP hợp lệ.");
        }
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
    : RoomBusinessRuleException("Chỉ Admin được phép thay đổi cấu hình phòng.");

public sealed class RoomHasDependentsException()
    : RoomBusinessRuleException("Không thể xoá phòng đang có thiết bị/hợp đồng/booking gắn vào.");
