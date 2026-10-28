using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IContractRepository
{
    Task<ContractBookingDto?> GetBookingAsync(int bookingId);
    Task<bool> AddFromBookingAsync(Contracts contract, Notifications customerNotification);
    Task<List<ContractDto>> GetAllAsync(int? customerId, int? ownerId);
    Task<ContractDto?> GetByIdAsync(int contractId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int contractId);
}
