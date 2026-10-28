using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IContractService
{
    Task<ContractDto> CreateFromBookingAsync(CreateContractDto dto, int userId, string role);
    Task<List<ContractDto>> GetAllAsync(int userId, string role);
    Task<ContractDto?> GetByIdAsync(int id, int userId, string role);
}
