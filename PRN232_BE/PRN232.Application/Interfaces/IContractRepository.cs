using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IContractRepository
{
    Task<ContractMutationResult> SaveMeterHandoverAsync(
        int contractId, int managerId, DateOnly handoverDate,
        decimal initialElectricReading, decimal initialWaterReading,
        DateOnly? expectedHandoverDate, decimal? expectedElectricReading, decimal? expectedWaterReading,
        DateOnly today, CancellationToken cancellationToken);
    Task<ContractBookingReadModel?> GetBookingAsync(int bookingId);
    Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId);
    Task<bool> AddFromBookingAsync(Contracts contract, Notifications customerNotification);
    Task<List<ContractReadModel>> GetAllAsync(int? customerId, int? ownerId);
    Task<ContractReadModel?> GetByIdAsync(int contractId, int? customerId, int? ownerId);
    Task<List<InvoiceReadModel>> GetIssuedInvoicesForTimelineAsync(
        int contractId,
        CancellationToken cancellationToken);
    Task<ContractMutationResult> ExtendAsync(
        int contractId,
        int managerId,
        DateOnly newEndDate,
        DateOnly today,
        DateTimeOffset asOfUtc,
        Notifications customerNotification,
        CancellationToken cancellationToken);
    Task<ContractMutationResult> TerminateAsync(
        int contractId,
        int managerId,
        bool roomReadyForRental,
        bool outstandingDebtAcknowledged,
        DateTimeOffset asOfUtc,
        Notifications customerNotification,
        CancellationToken cancellationToken);
    Task<ContractMutationStatus> MarkTerminatedRoomReadyAsync(
        int contractId,
        int managerId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);
    Task<bool> ExistsAsync(int contractId);
}
