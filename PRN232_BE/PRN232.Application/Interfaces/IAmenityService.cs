using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IAmenityService
{
    Task<List<AmenityDto>> GetAllAsync();
    Task<AmenityDto?> GetByIdAsync(int id);
    Task<AmenityDto> CreateAsync(CreateAmenityDto dto);
    Task<AmenityDto?> UpdateAsync(int id, UpdateAmenityDto dto);
    Task<bool> DeleteAsync(int id);
}
