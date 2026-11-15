using PRN232.Application.DTOs;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IContractTransferRepository
{
    Task<TransferUserReadModel?> FindUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task<string?> GetActiveOccupantNameAsync(int contractId, int occupantId, CancellationToken cancellationToken);
    Task<ContractTransferReadModel?> GetAsync(int transferId, CancellationToken cancellationToken);

    // customerId: đề nghị mà người này là người đứng tên cũ HOẶC người nhận. managerId: khu trọ đang phụ trách.
    Task<List<ContractTransferReadModel>> ListAsync(int? customerId, int? managerId, CancellationToken cancellationToken);

    // false nếu hợp đồng đã có một đề nghị đang chờ (chỉ mục duy nhất).
    Task<bool> AddAsync(ContractTransfers transfer, IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken);
    Task<bool> ConfirmAsync(int transferId, bool asOldHolder, DateTime atUtc, IReadOnlyList<Notifications> notifications,
        CancellationToken cancellationToken);
    Task<bool> CancelAsync(int transferId, int userId, DateTime atUtc, string? reason, IReadOnlyList<Notifications> notifications,
        CancellationToken cancellationToken);

    Task<ContractTransferFactsReadModel> GetFactsAsync(int fromContractId, int? becomingHolderOccupantId, DateOnly effectiveDate,
        CancellationToken cancellationToken);

    // Kết thúc hợp đồng cũ, tạo hợp đồng mới (cọc, chỉ số điện nước, biên bản, người ở cùng chuyển sang).
    // Trả về id hợp đồng mới, hoặc null nếu đề nghị/hợp đồng vừa thay đổi.
    Task<int?> CompleteAsync(ContractTransferCompletion completion, IReadOnlyList<Notifications> notifications,
        CancellationToken cancellationToken);
}

public interface IContractTransferService
{
    Task<List<ContractTransferDto>> ListAsync(int userId, string role, CancellationToken cancellationToken);
    Task<ContractTransferDto> CreateAsync(CreateContractTransferDto dto, int userId, string role, CancellationToken cancellationToken);
    Task<ContractTransferDto> ConfirmAsync(int transferId, int userId, string role, CancellationToken cancellationToken);
    Task<ContractTransferDto> CancelAsync(int transferId, CancelContractTransferDto dto, int userId, string role,
        CancellationToken cancellationToken);
    Task<ContractTransferDto> CompleteAsync(int transferId, int userId, string role, CancellationToken cancellationToken);
}
