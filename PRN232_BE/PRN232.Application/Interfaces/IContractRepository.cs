using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IContractRepository
{
    Task<ContractBookingReadModel?> GetBookingAsync(int bookingId);
    Task<bool> AddFromBookingAsync(Contracts contract, Notifications customerNotification);
    Task<List<ContractReadModel>> GetAllAsync(int? customerId, int? ownerId);
    Task<ContractReadModel?> GetByIdAsync(int contractId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int contractId);
}
