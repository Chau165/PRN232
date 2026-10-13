using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IIdentityDocumentRepository
{
    Task<List<IdentityDocumentDto>> GetAllAsync(string? status);
    Task<List<IdentityDocumentDto>> GetByCustomerIdAsync(int customerId);
    Task<IdentityDocumentDto?> GetByIdAsync(int id);
    Task<IdentityDocuments?> GetEntityByIdAsync(int id);

    // Chặn nộp trùng: 1 customer không được có 2 hồ sơ đang Pending/Approved cùng lúc.
    Task<bool> HasPendingOrApprovedAsync(int customerId);

    // Chặn 2 customer khác nhau dùng cùng 1 số CCCD.
    Task<bool> CccdNumberUsedByOtherAsync(string cccdNumber, int customerId);

    Task<IdentityDocuments> AddAsync(IdentityDocuments document);
    Task SaveChangesAsync();
}
