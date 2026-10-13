using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPropertyService
{
    Task<List<PropertyDto>> GetAllAsync(int? ownerId);
    Task<PropertyDto?> GetByIdAsync(int id);
    Task<PropertyDto> CreateAsync(CreatePropertyDto dto, int ownerId);
    Task<PropertyDto?> UpdateAsync(int id, UpdatePropertyDto dto, int requestingUserId, bool isAdmin);
    Task<bool> DeleteAsync(int id, int requestingUserId, bool isAdmin);
}
