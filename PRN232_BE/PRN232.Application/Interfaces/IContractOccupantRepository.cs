using PRN232.Application.DTOs;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IContractOccupantRepository
{
    Task<List<ContractOccupantReadModel>> GetAsync(int contractId, CancellationToken cancellationToken);
    Task<int> GetRoomMaxOccupantsAsync(int roomId, CancellationToken cancellationToken);

    // Kiểm tra lại sức chứa trong transaction (khóa dòng hợp đồng): false nếu đã đủ người.
    Task<bool> AddAsync(ContractOccupants occupant, int maxCoOccupants, IReadOnlyList<Notifications> notifications,
        CancellationToken cancellationToken);

    // Chỉ ghi khi người đó còn đang ở (MoveOutDate = null).
    Task<bool> RecordMoveOutAsync(int contractId, int occupantId, DateOnly moveOutDate,
        IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken);
}

public interface IContractOccupantService
{
    Task<ContractOccupantsDto> GetAsync(int contractId, int userId, string role, CancellationToken cancellationToken);
    Task<ContractOccupantsDto> AddAsync(int contractId, AddContractOccupantDto dto, int userId, string role,
        CancellationToken cancellationToken);
    Task<ContractOccupantsDto> RecordMoveOutAsync(int contractId, int occupantId, RecordOccupantMoveOutDto dto,
        int userId, string role, CancellationToken cancellationToken);
    Task<RevealedIdentityDto> RevealIdentityAsync(int contractId, int occupantId, int userId, string role,
        CancellationToken cancellationToken);
}
