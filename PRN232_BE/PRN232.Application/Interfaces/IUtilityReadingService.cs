using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IUtilityReadingService
{
    Task<UtilityReadingDto> CreateAsync(CreateUtilityReadingDto dto, int userId, string role);
    Task<List<UtilityReadingDto>> GetAllAsync(int userId, string role, int? contractId);
    Task<UtilityReadingDto?> GetByIdAsync(int id, int userId, string role);
}
