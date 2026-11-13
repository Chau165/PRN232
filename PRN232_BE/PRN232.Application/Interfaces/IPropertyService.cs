using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPropertyService
{
    Task<PropertyServiceRateDto> CreateServiceRateAsync(int id, CreatePropertyServiceRateDto dto, CancellationToken cancellationToken);
    Task<List<PropertyServiceRateDto>?> GetServiceRatesAsync(int id, DateOnly? month, int requestingUserId, bool isAdmin, CancellationToken cancellationToken);
    Task<List<PropertyDto>> GetAllAsync(int? ownerId, int? managerId);
    Task<PropertyDto?> GetByIdAsync(int id, int requestingUserId, bool isManager);
    Task<PropertyDto> CreateAsync(
        CreatePropertyDto dto,
        int ownerId,
        bool isAdmin,
        PropertyImageInput? image = null);
    Task<PropertyDto?> UpdateAsync(
        int id,
        UpdatePropertyDto dto,
        int requestingUserId,
        bool isAdmin,
        PropertyImageInput? image = null);
    Task<bool> DeleteAsync(int id, int requestingUserId, bool isAdmin);
}
