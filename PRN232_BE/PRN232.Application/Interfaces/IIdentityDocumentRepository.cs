using PRN232.Application.ReadModels;
using DomainIdentityDocument = PRN232.Domain.Entities.IdentityDocuments;

namespace PRN232.Application.Interfaces;

public interface IIdentityDocumentRepository
{
    Task<List<IdentityDocumentReadModel>> GetAllAsync(string? status);
    Task<List<IdentityDocumentReadModel>> GetByCustomerIdAsync(int customerId);
    Task<IdentityDocumentReadModel?> GetByIdAsync(int id);
    Task<DomainIdentityDocument?> GetTrackedByIdAsync(int id);

    // Chặn nộp trùng: 1 customer không được có 2 hồ sơ đang Pending/Approved cùng lúc.
    Task<bool> HasPendingOrApprovedAsync(int customerId);

    // Chặn 2 customer khác nhau dùng cùng 1 số CCCD.
    Task<bool> CccdNumberUsedByOtherAsync(string cccdNumber, int customerId);

    Task<DomainIdentityDocument> AddAsync(DomainIdentityDocument document);
    Task SaveChangesAsync();
}
