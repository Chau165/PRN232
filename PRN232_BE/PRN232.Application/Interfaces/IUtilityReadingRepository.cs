using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IUtilityReadingRepository
{
    Task<ReadingContractDto?> GetContractAsync(int contractId);
    Task<LatestReadingDto?> GetLatestAsync(int contractId);
    Task<bool> AddIfLatestAsync(UtilityReadings reading);
    Task<List<UtilityReadingDto>> GetAllAsync(int? customerId, int? ownerId, int? contractId);
    Task<UtilityReadingDto?> GetByIdAsync(int readingId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int readingId);
}
