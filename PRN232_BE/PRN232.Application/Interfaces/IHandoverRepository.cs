using PRN232.Application.DTOs;
using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

public interface IHandoverRepository
{
    Task<HandoverContractReadModel?> GetContractAsync(int contractId, CancellationToken cancellationToken);
    Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId, CancellationToken cancellationToken);
    Task<List<HandoverEquipmentReadModel>> GetRoomEquipmentsAsync(int roomId, CancellationToken cancellationToken);
    Task<List<HandoverRecordReadModel>> GetRecordsAsync(int contractId, CancellationToken cancellationToken);

    // Tạo mới hoặc thay toàn bộ biên bản cùng loại. false nếu một lần lưu khác vừa chen vào.
    Task<bool> SaveAsync(int contractId, string kind, DateOnly handoverDate, string? note, int recordedById,
        DateTime recordedAtUtc, IReadOnlyList<HandoverItemWriteModel> items, CancellationToken cancellationToken);
}

public interface IHandoverService
{
    Task<ContractHandoversDto> GetAsync(int contractId, int userId, string role, CancellationToken cancellationToken);
    Task<ContractHandoversDto> SaveAsync(int contractId, string kind, SaveHandoverDto dto, int userId, string role,
        CancellationToken cancellationToken);
}
