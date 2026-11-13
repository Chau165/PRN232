using PRN232.Application.ReadModels;
using DomainIdentityDocument = PRN232.Domain.Entities.IdentityDocuments;

namespace PRN232.Application.Interfaces;

public interface IIdentityDocumentRepository
{
    // managerId = null: every document (Admin). With managerId: only documents of customers who
    // have a non-cancelled booking or a contract on a property assigned to that Manager (A3).
    Task<List<IdentityDocumentReadModel>> GetAllAsync(string? status, int? managerId);
    Task<List<IdentityDocumentReadModel>> GetByCustomerIdAsync(int customerId);
    Task<IdentityDocumentReadModel?> GetByIdAsync(int id);
    Task<DomainIdentityDocument?> GetTrackedByIdAsync(int id);

    // Whether the customer has a non-cancelled booking or a contract on a property assigned to
    // this Manager through PropertyManagerAssignments (A3).
    Task<bool> IsCustomerOfManagerAsync(int customerId, int managerId);

    // Chặn nộp trùng: 1 customer không được có 2 hồ sơ đang Pending/Approved cùng lúc.
    Task<bool> HasPendingOrApprovedAsync(int customerId);

    // Chặn 2 customer khác nhau dùng cùng 1 số CCCD. So theo hash (ICccdHasher) vì cột
    // CCCDNumber đã mã hóa ngẫu nhiên, không so sánh trực tiếp được (A9).
    Task<bool> CccdNumberUsedByOtherAsync(string cccdNumberHash, int customerId);

    Task<DomainIdentityDocument> AddAsync(DomainIdentityDocument document);
    Task SaveChangesAsync();
}
