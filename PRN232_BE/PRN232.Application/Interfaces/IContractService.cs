using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IContractService
{
    Task<ContractDto> SaveMeterHandoverAsync(int contractId, SaveContractMeterHandoverDto request,
        int userId, string role, CancellationToken cancellationToken);
    Task<ContractDto> CreateFromBookingAsync(CreateContractDto dto, int userId, string role);
    Task<List<ContractDto>> GetAllAsync(int userId, string role);
    Task<ContractDto?> GetByIdAsync(int id, int userId, string role);
    Task<ContractBillingTimelineDto?> GetBillingTimelineAsync(
        int contractId,
        int userId,
        string role,
        CancellationToken cancellationToken);
    Task<ContractDto> ExtendContractAsync(
        int contractId,
        ExtendContractRequestDto request,
        int userId,
        string role,
        CancellationToken cancellationToken);
    Task<TerminateContractResponseDto> TerminateContractAsync(
        int contractId,
        TerminateContractRequestDto request,
        int userId,
        string role,
        CancellationToken cancellationToken);
    Task MarkTerminatedRoomReadyAsync(
        int contractId,
        int userId,
        string role,
        CancellationToken cancellationToken);
}
