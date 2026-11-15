using PRN232.Application.Policies;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Normalizers;
using PRN232.Application.ReadModels;

namespace PRN232.Application.Services;

public class PropertyService : IPropertyService
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private readonly IPropertyRepository _repository;
    private readonly IAdministrativeDivisionCatalog? _administrativeDivisionCatalog;
    private readonly IPropertyImageStorage? _imageStorage;

    public PropertyService(
        IPropertyRepository repository,
        IAdministrativeDivisionCatalog? administrativeDivisionCatalog = null,
        IPropertyImageStorage? imageStorage = null)
    {
        _repository = repository;
        _administrativeDivisionCatalog = administrativeDivisionCatalog;
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

    //NGHIA
    public async Task<List<PropertyServiceRateDto>?> GetServiceRatesAsync(
        int id, DateOnly? month, int requestingUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (month.HasValue && month.Value.Day != 1)
        {
            throw new PropertyValidationException("Month must be the first day of a month (yyyy-MM-01).");
        }

        var property = await _repository.GetByIdAsync(id);
        if (property is null)
        {
            return null;
        }
        if (!isAdmin && !await _repository.IsManagerAssignedAsync(id, requestingUserId))
        {
            throw new PropertyForbiddenException();
        }

        var rates = await _repository.GetServiceRatesAsync(id, month, cancellationToken);
        return rates.Select(rate => new PropertyServiceRateDto(
            rate.RateId, rate.PropertyId, rate.ServiceId, rate.ServiceName, rate.Price,
            rate.UnitCode, rate.BillingBasis, rate.EffectiveFrom, rate.EffectiveTo)).ToList();
    }

    public async Task<PropertyServiceRateDto> CreateServiceRateAsync(
        int id, CreatePropertyServiceRateDto dto, CancellationToken cancellationToken)
    {
        if (dto.ServiceId <= 0 || dto.Price < 0 || dto.Price > 9999999999999999.99m ||
            decimal.Round(dto.Price, 2) != dto.Price)
        {
            throw new PropertyValidationException("ServiceId must be positive; price must be nonnegative with at most two decimal places and fit numeric(18,2).");
        }
        if (dto.EffectiveFrom == default || dto.EffectiveFrom.Day != 1 ||
            (dto.EffectiveTo.HasValue && (dto.EffectiveTo.Value.Day != 1 || dto.EffectiveTo <= dto.EffectiveFrom)))
        {
            throw new PropertyValidationException("Effective dates must be month starts; EffectiveTo must be after EffectiveFrom.");
        }
        var validBasis = (dto.UnitCode, dto.BillingBasis) is
            ("kwh", "usage") or ("m3", "usage") or ("room_month", "fixed_room") or
            ("person_month", "occupant") or ("vehicle_month", "vehicle");
        if (!validBasis)
        {
            throw new PropertyValidationException("Invalid unitCode/billingBasis pair.");
        }
        if (await _repository.GetByIdAsync(id) is null)
        {
            throw new PropertyRateNotFoundException("Property does not exist.");
        }
        if (!await _repository.ServiceExistsAsync(dto.ServiceId, cancellationToken))
        {
            throw new PropertyRateNotFoundException("Service does not exist.");
        }

        var rate = new PRN232.Domain.Entities.PropertyServiceRates
        {
            PropertyId = id, ServiceId = dto.ServiceId, Price = dto.Price,
            UnitCode = dto.UnitCode, BillingBasis = dto.BillingBasis,
            EffectiveFrom = dto.EffectiveFrom, EffectiveTo = dto.EffectiveTo
        };
        if (!await _repository.AddServiceRateIfNoOverlapAsync(rate, cancellationToken))
        {
            throw new PropertyRateConflictException("This period overlaps an existing rate for this property's service. Existing rates were not changed.");
        }
        var rates = await _repository.GetServiceRatesAsync(id, null, cancellationToken);
        var created = rates.Single(item => item.RateId == rate.RateId);
        return new PropertyServiceRateDto(created.RateId, created.PropertyId, created.ServiceId,
            created.ServiceName, created.Price, created.UnitCode, created.BillingBasis,
            created.EffectiveFrom, created.EffectiveTo);
    }

    //Nghia
    public async Task<PropertyDto> CreateAsync(
        CreatePropertyDto dto,
        int ownerId,
        bool isAdmin,
        PropertyImageInput? image = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(isAdmin);
        ValidatePropertyText(dto.PropertyName, dto.AddressText);
        var (province, commune) = ResolveAdministrativeLocation(dto.ProvinceCode, dto.WardCode);
        ValidateStreetAddressDoesNotRepeatArea(dto.AddressText, commune.Name, province.Name);
        var coordinates = PropertyCoordinatePolicy.Require(dto.Latitude, dto.Longitude);
        string? uploadedImageUrl = null;
        try
        {
            uploadedImageUrl = await UploadImageAsync(image);
            var property = ConvertToEntity(
                dto,
                ownerId,
                commune,
                province,
                coordinates,
                uploadedImageUrl);
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
        PropertyImageInput? image = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(isAdmin);
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        ValidatePropertyName(dto.PropertyName);
        var hasProvinceCode = !string.IsNullOrWhiteSpace(dto.ProvinceCode);
        var hasWardCode = !string.IsNullOrWhiteSpace(dto.WardCode);
        if (hasProvinceCode != hasWardCode)
        {
            throw new PropertyValidationException("Hãy chọn cả tỉnh/thành phố và phường/xã từ danh mục.");
        }

        // Không gửi mã: giữ nguyên vị trí đã lưu (chỉ hợp lệ khi số nhà, tên đường không đổi).
        // Có mã: lấy tên chuẩn từ danh mục và bỏ cấp quận/huyện (địa chỉ sau sáp nhập).
        var provinceCode = entity.ProvinceCode;
        var wardCode = entity.WardCode;
        var provinceName = entity.City;
        var wardName = entity.Ward;
        var districtName = entity.District;
        if (hasProvinceCode && hasWardCode)
        {
            var administrativeLocation = ResolveAdministrativeLocation(dto.ProvinceCode, dto.WardCode);
            provinceCode = administrativeLocation.Province.Code;
            wardCode = administrativeLocation.Commune.Code;
            provinceName = administrativeLocation.Province.Name;
            wardName = administrativeLocation.Commune.Name;
            districtName = string.Empty;
        }

        var addressUnchanged = IsSameAddress(
            entity,
            dto.AddressText,
            wardName,
            provinceName);
        var codesChanged = hasProvinceCode && (dto.ProvinceCode != entity.ProvinceCode || dto.WardCode != entity.WardCode);
        var suppliedCoordinates = dto.Latitude.HasValue || dto.Longitude.HasValue;
        var updateLocation = !addressUnchanged || codesChanged || suppliedCoordinates;
        AddressCoordinates coordinates;
        if (updateLocation)
        {
            if ((!addressUnchanged || codesChanged) && (!hasProvinceCode || !hasWardCode))
                throw new PropertyValidationException("Địa chỉ mới cần chọn tỉnh/thành phố và phường/xã hiện hành.");
            ValidateStreetAddress(dto.AddressText);
            ValidateStreetAddressDoesNotRepeatArea(dto.AddressText, wardName, provinceName);
            coordinates = PropertyCoordinatePolicy.Require(dto.Latitude, dto.Longitude);
        }
        else coordinates = new AddressCoordinates(entity.Latitude, entity.Longitude);

        var previousImageUrl = entity.PrimaryImageUrl;
        string? uploadedImageUrl = null;
        try
        {
            uploadedImageUrl = await UploadImageAsync(image);
            ApplyUpdateToEntity(
                dto,
                entity,
                provinceCode,
                wardCode,
                wardName,
                districtName,
                provinceName,
                coordinates,
                updateLocation,
                uploadedImageUrl ?? previousImageUrl);

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

    // Quận/huyện không còn tham gia so sánh: gán mã cho khu trọ cũ (cùng phường, cùng số nhà)
    // không làm đổi vị trí nên không cần geocode lại.
    private static bool IsSameAddress(
        PRN232.Domain.Entities.Property entity,
        string addressText,
        string ward,
        string city) =>
        string.Equals(entity.AddressText.Trim(), addressText.Trim(), StringComparison.Ordinal) &&
        string.Equals(entity.Ward.Trim(), ward.Trim(), StringComparison.Ordinal) &&
        string.Equals(entity.City.Trim(), city.Trim(), StringComparison.Ordinal);

    private static void EnsureAdmin(bool isAdmin)
    {
        if (!isAdmin)
        {
            throw new PropertyForbiddenException();
        }
    }

    private static void ValidatePropertyText(string propertyName, string addressText)
    {
        ValidatePropertyName(propertyName);
        ValidateStreetAddress(addressText);
    }

    private static void ValidatePropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new PropertyValidationException("Tên khu trọ không được để trống.");
        }
    }

    private static void ValidateStreetAddress(string addressText)
    {
        if (string.IsNullOrWhiteSpace(addressText))
        {
            throw new PropertyValidationException("Số nhà và tên đường không được để trống.");
        }
    }

    private (AdministrativeDivisionReadModel Province, AdministrativeDivisionReadModel Commune)
        ResolveAdministrativeLocation(string? provinceCode, string? wardCode)
    {
        if (_administrativeDivisionCatalog is null)
        {
            throw new PropertyValidationException("Danh mục hành chính hiện không khả dụng.");
        }

        if (string.IsNullOrWhiteSpace(provinceCode) || string.IsNullOrWhiteSpace(wardCode))
        {
            throw new PropertyValidationException("Hãy chọn tỉnh/thành phố và phường/xã từ danh mục.");
        }

        var province = _administrativeDivisionCatalog.GetProvince(provinceCode);
        var commune = _administrativeDivisionCatalog.GetCommune(wardCode);
        if (province is null)
        {
            throw new PropertyValidationException("Mã tỉnh/thành phố không có trong danh mục hiện hành.");
        }

        if (commune is null)
        {
            throw new PropertyValidationException("Mã phường/xã không có trong danh mục hiện hành.");
        }

        if (!string.Equals(commune.ParentProvinceCode, province.Code, StringComparison.Ordinal))
        {
            throw new PropertyValidationException("Phường/xã không thuộc tỉnh/thành phố đã chọn.");
        }

        return (province, commune);
    }

    private static void ValidateStreetAddressDoesNotRepeatArea(
        string addressText,
        string wardName,
        string provinceName)
    {
        var administrativeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            PublicAreaNameNormalizer.Normalize(wardName),
            PublicAreaNameNormalizer.Normalize(provinceName)
        };
        var repeatedName = addressText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(PublicAreaNameNormalizer.Normalize)
            .Any(administrativeNames.Contains);
        if (repeatedName)
        {
            throw new PropertyValidationException(
                "Số nhà và tên đường chỉ nhập địa chỉ chi tiết; không lặp lại phường/xã hoặc tỉnh/thành phố.");
        }
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
        AdministrativeDivisionReadModel commune,
        AdministrativeDivisionReadModel province,
        AddressCoordinates coordinates,
        string? imageUrl)
    {
        return new PRN232.Domain.Entities.Property
        {
            OwnerId = ownerId,
            PropertyName = dto.PropertyName.Trim(),
            AddressText = dto.AddressText.Trim(),
            Ward = commune.Name,
            District = string.Empty,
            City = province.Name,
            ProvinceCode = province.Code,
            WardCode = commune.Code,
            Latitude = coordinates.Latitude,
            Longitude = coordinates.Longitude,
            PrimaryImageUrl = imageUrl,
            Description = NormalizeOptional(dto.Description)
        };
    }

    private static void ApplyUpdateToEntity(
        UpdatePropertyDto dto,
        PRN232.Domain.Entities.Property entity,
        string provinceCode,
        string wardCode,
        string wardName,
        string districtName,
        string provinceName,
        AddressCoordinates coordinates,
        bool updateLocation,
        string? imageUrl)
    {
        entity.PropertyName = dto.PropertyName.Trim();
        // Mã và tên đơn vị hành chính luôn được ghi (không gửi mã thì đây là giá trị đang lưu), để
        // khu trọ cũ được gán mã ngay khi Admin chọn lại tỉnh/phường mà không phải đổi vị trí.
        entity.ProvinceCode = provinceCode;
        entity.WardCode = wardCode;
        entity.Ward = wardName;
        entity.District = districtName;
        entity.City = provinceName;
        if (updateLocation)
        {
            entity.AddressText = dto.AddressText.Trim();
            entity.Latitude = coordinates.Latitude;
            entity.Longitude = coordinates.Longitude;
        }
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
            readModel.Description,
            readModel.ProvinceCode,
            readModel.WardCode);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

public sealed class PropertyRateNotFoundException(string message) : PropertyBusinessRuleException(message);
public sealed class PropertyRateConflictException(string message) : PropertyBusinessRuleException(message);

public sealed class PropertyValidationException(string message) : PropertyBusinessRuleException(message);

public class PropertyGeocodingException(
    string message = "Dịch vụ xác định tọa độ hiện không khả dụng.")
    : Exception(message);

public sealed class PropertyGeocodingRateLimitedException()
    : PropertyGeocodingException("Dịch vụ định vị đang giới hạn số lượt truy cập. Vui lòng thử lại sau.");

public sealed class PropertyImageStorageException()
    : Exception("Dịch vụ lưu trữ ảnh hiện không khả dụng.");

public sealed class PropertyForbiddenException()
    : PropertyBusinessRuleException("Bạn không phải chủ sở hữu của khu trọ này.");

public sealed class PropertyHasRoomsException()
    : PropertyBusinessRuleException("Không thể xoá khu trọ còn dữ liệu liên quan cần bảo toàn.");
