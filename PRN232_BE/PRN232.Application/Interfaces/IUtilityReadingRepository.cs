using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IUtilityReadingRepository
{
    Task<ReadingContractReadModel?> GetContractAsync(int contractId);
    Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId);
    Task<LatestReadingReadModel?> GetLatestAsync(int contractId);
    Task<bool> AddIfLatestAsync(UtilityReadings reading, int managerId = 0);
    Task<List<UtilityReadingReadModel>> GetAllAsync(int? customerId, int? ownerId, int? contractId);
    Task<UtilityReadingReadModel?> GetByIdAsync(int readingId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int readingId);
}
