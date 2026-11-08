using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;

namespace PRN232.Application.Services;

public class PropertyService : IPropertyService
{
    private readonly IPropertyRepository _repository;

    public PropertyService(IPropertyRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PropertyDto>> GetAllAsync(int? ownerId)
    {
        var properties = await _repository.GetAllAsync(ownerId);
        return properties.Select(ConvertToDto).ToList();
    }

    public async Task<PropertyDto?> GetByIdAsync(int id)
    {
        var property = await _repository.GetByIdAsync(id);
        if (property is null)
        {
            return null;
        }
        return ConvertToDto(property);
    }

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
        if (!isAdmin && property.OwnerId != requestingUserId)
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

    public async Task<PropertyDto> CreateAsync(CreatePropertyDto dto, int ownerId)
    {
        if (string.IsNullOrWhiteSpace(dto.PropertyName))
        {
            throw new PropertyValidationException("Tên khu trọ không được để trống.");
        }
        if (string.IsNullOrWhiteSpace(dto.AddressText))
        {
            throw new PropertyValidationException("Địa chỉ không được để trống.");
        }

        var property = ConvertToEntity(dto, ownerId);

        var created = await _repository.AddAsync(property);
        await _repository.SaveChangesAsync();

        return ConvertToDto(await GetPropertyResponseAsync(created.PropertyId));
    }

    public async Task<PropertyDto?> UpdateAsync(int id, UpdatePropertyDto dto, int requestingUserId, bool isAdmin)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        if (!isAdmin && entity.OwnerId != requestingUserId)
        {
            throw new PropertyForbiddenException();
        }
        if (string.IsNullOrWhiteSpace(dto.PropertyName))
        {
            throw new PropertyValidationException("Tên khu trọ không được để trống.");
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
        if (!isAdmin && entity.OwnerId != requestingUserId)
        {
            throw new PropertyForbiddenException();
        }
        if (await _repository.HasRoomsAsync(id))
        {
            throw new PropertyHasRoomsException();
        }

        _repository.Remove(entity);
        await _repository.SaveChangesAsync();
        return true;
    }

    private static PRN232.Domain.Entities.Property ConvertToEntity(CreatePropertyDto dto, int ownerId)
    {
        return new PRN232.Domain.Entities.Property
        {
            OwnerId = ownerId,
            PropertyName = dto.PropertyName.Trim(),
            AddressText = dto.AddressText.Trim(),
            Ward = dto.Ward?.Trim() ?? string.Empty,
            District = dto.District?.Trim() ?? string.Empty,
            City = dto.City?.Trim() ?? string.Empty,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };
    }

    private static void ApplyUpdateToEntity(UpdatePropertyDto dto, PRN232.Domain.Entities.Property entity)
    {
        entity.PropertyName = dto.PropertyName.Trim();
        entity.AddressText = dto.AddressText.Trim();
        entity.Ward = dto.Ward?.Trim() ?? string.Empty;
        entity.District = dto.District?.Trim() ?? string.Empty;
        entity.City = dto.City?.Trim() ?? string.Empty;
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;
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
            readModel.RoomCount);
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

public sealed class PropertyForbiddenException()
    : PropertyBusinessRuleException("Bạn không phải chủ sở hữu của khu trọ này.");

public sealed class PropertyHasRoomsException()
    : PropertyBusinessRuleException("Không thể xoá khu trọ còn phòng — xoá hết phòng trước.");
