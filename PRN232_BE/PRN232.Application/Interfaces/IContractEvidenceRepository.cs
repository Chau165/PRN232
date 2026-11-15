using PRN232.Application.DTOs;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IContractEvidenceRepository
{
    Task<List<ContractEvidenceFileReadModel>> GetFilesAsync(int contractId, CancellationToken cancellationToken);
    Task<DateOnly?> GetMoveInHandoverDateAsync(int contractId, CancellationToken cancellationToken);

    // false khi đã đủ số file tối đa của loại này (kiểm tra lại trong transaction).
    Task<bool> AddAsync(ContractEvidenceFiles file, int maxFilesPerKind, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int fileId, CancellationToken cancellationToken);
    Task<bool> HideAsync(int fileId, int adminId, string reason, DateTime hiddenAtUtc, CancellationToken cancellationToken);
}

// Bucket private trên Supabase; tests thay bằng bộ lưu trữ trong bộ nhớ.
public interface IContractEvidenceStorage
{
    Task<string> UploadAsync(int contractId, string kind, string contentType, byte[] content);
    Task<string> GetReadUrlAsync(string objectPath);
    Task DeleteAsync(string objectPath);
}

public interface IContractEvidenceService
{
    Task<ContractEvidenceDto> GetAsync(int contractId, int userId, string role, CancellationToken cancellationToken);
    Task<ContractEvidenceDto> UploadAsync(int contractId, string kind, ContractEvidenceUpload upload, int userId,
        string role, CancellationToken cancellationToken);
    Task<ContractEvidenceDto> DeleteAsync(int contractId, int fileId, int userId, string role, CancellationToken cancellationToken);
    Task<ContractEvidenceDto> HideAsync(int contractId, int fileId, HideContractEvidenceDto dto, int userId, string role,
        CancellationToken cancellationToken);
}
