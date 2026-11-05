using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;

namespace PRN232.Application.Services;

public class PropertyService : IPropertyService
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private readonly IPropertyRepository _repository;
    private readonly IPropertyGeocodingService? _geocodingService;
    private readonly IPropertyImageStorage? _imageStorage;

    public PropertyService(
        IPropertyRepository repository,
        IPropertyGeocodingService? geocodingService = null,
        IPropertyImageStorage? imageStorage = null)
    {
        _repository = repository;
        _geocodingService = geocodingService;
        _imageStorage = imageStorage;
    }

    public async Task<List<PropertyDto>> GetAllAsync(int? ownerId, int? managerId)
    {
        var properties = await _repository.GetAllAsync(ownerId, managerId);
        return properties.Select(ConvertToDto).ToList();
    }

    public async Task<PropertyDto?> GetByIdAsync(int id, int requestingUserId, bool isManager)
    {
        var property = await _repository.GetByIdAsync(id);
        if (property is null)
        {
            return null;
        }
        if (isManager && !await _repository.IsManagerAssignedAsync(id, requestingUserId))
        {
            throw new PropertyForbiddenException();
        }
        return ConvertToDto(property);
    }

    public async Task<PropertyDto> CreateAsync(
        CreatePropertyDto dto,
        int ownerId,
        bool isAdmin,
        PropertyImageInput? image = null)
    {
        EnsureAdmin(isAdmin);
        ValidatePropertyText(dto.PropertyName, dto.AddressText);

        var coordinates = await ResolveCoordinatesAsync(dto.AddressText, dto.Ward, dto.District, dto.City);
        string? uploadedImageUrl = null;
        try
        {
            uploadedImageUrl = await UploadImageAsync(image);
            var property = ConvertToEntity(dto, ownerId, coordinates, uploadedImageUrl);
            var created = await _repository.AddAsync(property);
            await _repository.SaveChangesAsync();

            return ConvertToDto(await GetPropertyResponseAsync(created.PropertyId));
        }
        catch
        {
            await TryDeleteImageAsync(uploadedImageUrl);
            throw;
        }
    }

    public async Task<PropertyDto?> UpdateAsync(
        int id,
        UpdatePropertyDto dto,
        int requestingUserId,
        bool isAdmin,
        PropertyImageInput? image = null)
    {
        EnsureAdmin(isAdmin);
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        ValidatePropertyText(dto.PropertyName, dto.AddressText);

        var coordinates = await ResolveCoordinatesAsync(dto.AddressText, dto.Ward, dto.District, dto.City);
        var previousImageUrl = entity.PrimaryImageUrl;
        string? uploadedImageUrl = null;
        try
        {
            uploadedImageUrl = await UploadImageAsync(image);
            ApplyUpdateToEntity(dto, entity, coordinates, uploadedImageUrl ?? previousImageUrl);

            await _repository.SaveChangesAsync();
            var updated = await _repository.GetByIdAsync(id);
            if (updated is null)
            {
                throw new InvalidOperationException("Updated property was not found.");
            }

            if (uploadedImageUrl is not null && previousImageUrl is not null)
            {
                await TryDeleteImageAsync(previousImageUrl);
            }
            return ConvertToDto(updated);
        }
        catch
        {
            await TryDeleteImageAsync(uploadedImageUrl);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id, int requestingUserId, bool isAdmin)
    {
        EnsureAdmin(isAdmin);
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return false;
        }
        if (await _repository.HasDependentsAsync(id))
        {
            throw new PropertyHasRoomsException();
        }

        _repository.Remove(entity);
        await _repository.SaveChangesAsync();
        return true;
    }

    private static void EnsureAdmin(bool isAdmin)
    {
        if (!isAdmin)
        {
            throw new PropertyForbiddenException();
        }
    }

    private static void ValidatePropertyText(string propertyName, string addressText)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new PropertyValidationException("Tên khu trọ không được để trống.");
        }
        if (string.IsNullOrWhiteSpace(addressText))
        {
            throw new PropertyValidationException("Địa chỉ không được để trống.");
        }
    }

    private async Task<AddressCoordinates> ResolveCoordinatesAsync(
        string addressText,
        string? ward,
        string? district,
        string? city)
    {
        if (_geocodingService is null)
        {
            throw new PropertyGeocodingException();
        }

        var address = string.Join(", ", new[] { addressText, ward, district, city }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));
        AddressCoordinates? coordinates;
        try
        {
            coordinates = await _geocodingService.GeocodeAsync(address);
        }
        catch (PropertyGeocodingException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new PropertyGeocodingException();
        }

        if (coordinates is null)
        {
            throw new PropertyValidationException("Không thể xác định tọa độ từ địa chỉ đã nhập.");
        }
        ValidateCoordinates(coordinates);
        return coordinates;
    }

    private async Task<string?> UploadImageAsync(PropertyImageInput? image)
    {
        if (image is null)
        {
            return null;
        }
        ValidateImage(image);
        if (_imageStorage is null)
        {
            throw new PropertyImageStorageException();
        }
        try
        {
            return await _imageStorage.UploadAsync(image);
        }
        catch (PropertyImageStorageException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new PropertyImageStorageException();
        }
    }

    private async Task TryDeleteImageAsync(string? imageUrl)
    {
        if (imageUrl is null || _imageStorage is null)
        {
            return;
        }
        try
        {
            await _imageStorage.DeleteAsync(imageUrl);
        }
        catch
        {
            // Cleanup is best-effort so the original operation failure remains visible.
        }
    }

    private static PRN232.Domain.Entities.Property ConvertToEntity(
        CreatePropertyDto dto,
        int ownerId,
        AddressCoordinates coordinates,
        string? imageUrl)
    {
        return new PRN232.Domain.Entities.Property
        {
            OwnerId = ownerId,
            PropertyName = dto.PropertyName.Trim(),
            AddressText = dto.AddressText.Trim(),
            Ward = dto.Ward?.Trim() ?? string.Empty,
            District = dto.District?.Trim() ?? string.Empty,
            City = dto.City?.Trim() ?? string.Empty,
            Latitude = coordinates.Latitude,
            Longitude = coordinates.Longitude,
            PrimaryImageUrl = imageUrl,
            Description = NormalizeOptional(dto.Description)
        };
    }

    private static void ApplyUpdateToEntity(
        UpdatePropertyDto dto,
        PRN232.Domain.Entities.Property entity,
        AddressCoordinates coordinates,
        string? imageUrl)
    {
        entity.PropertyName = dto.PropertyName.Trim();
        entity.AddressText = dto.AddressText.Trim();
        entity.Ward = dto.Ward?.Trim() ?? string.Empty;
        entity.District = dto.District?.Trim() ?? string.Empty;
        entity.City = dto.City?.Trim() ?? string.Empty;
        entity.Latitude = coordinates.Latitude;
        entity.Longitude = coordinates.Longitude;
        entity.PrimaryImageUrl = imageUrl;
        entity.Description = NormalizeOptional(dto.Description);
    }

    private static PropertyDto ConvertToDto(PropertySummaryReadModel readModel)
    {
        return new PropertyDto(
            readModel.PropertyId,
            readModel.OwnerId,
            readModel.OwnerName,
            readModel.PropertyName,
            readModel.AddressText,
            readModel.Ward,
            readModel.District,
            readModel.City,
            readModel.Latitude,
            readModel.Longitude,
            readModel.RoomCount,
            readModel.PrimaryImageUrl,
            readModel.Description);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateCoordinates(AddressCoordinates coordinates)
    {
        if (coordinates.Latitude is < -90 or > 90 || coordinates.Longitude is < -180 or > 180)
        {
            throw new PropertyGeocodingException();
        }
    }

    private static void ValidateImage(PropertyImageInput image)
    {
        if (image.Content.Length is 0 or > MaxImageBytes)
        {
            throw new PropertyValidationException("Ảnh phải có dung lượng từ 1 byte đến 5 MiB.");
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var content = image.Content.AsSpan();
        var isJpeg = image.ContentType == "image/jpeg" &&
                     (extension is ".jpg" or ".jpeg") &&
                     content.Length >= 4 &&
                     content.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) &&
                     content[^2..].SequenceEqual(new byte[] { 0xFF, 0xD9 });
        var isPng = image.ContentType == "image/png" &&
                    extension == ".png" &&
                    content.Length >= 20 &&
                    content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) &&
                    content[^8..].SequenceEqual(new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 });
        var isWebp = image.ContentType == "image/webp" &&
                     extension == ".webp" &&
                     content.Length >= 20 &&
                     content[..4].SequenceEqual("RIFF"u8) &&
                     content[8..12].SequenceEqual("WEBP"u8) &&
                     System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(content[4..8]) == content.Length - 8;
        if (!isJpeg && !isPng && !isWebp)
        {
            throw new PropertyValidationException("Ảnh phải là tệp JPEG, PNG hoặc WebP hợp lệ.");
        }
    }

    private async Task<PropertySummaryReadModel> GetPropertyResponseAsync(int propertyId)
    {
        var property = await _repository.GetByIdAsync(propertyId);
        if (property is null)
        {
            throw new InvalidOperationException("Created property was not found.");
        }
        return property;
    }
}

public class PropertyBusinessRuleException(string message) : Exception(message);

public sealed class PropertyValidationException(string message) : PropertyBusinessRuleException(message);

public sealed class PropertyGeocodingException()
    : Exception("Dịch vụ xác định tọa độ hiện không khả dụng.");

public sealed class PropertyImageStorageException()
    : Exception("Dịch vụ lưu trữ ảnh hiện không khả dụng.");

public sealed class PropertyForbiddenException()
    : PropertyBusinessRuleException("Bạn không phải chủ sở hữu của khu trọ này.");

public sealed class PropertyHasRoomsException()
    : PropertyBusinessRuleException("Không thể xoá khu trọ còn dữ liệu liên quan cần bảo toàn.");
