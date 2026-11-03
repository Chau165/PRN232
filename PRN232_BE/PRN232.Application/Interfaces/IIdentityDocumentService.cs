using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IIdentityDocumentService
{
    Task<List<IdentityDocumentDto>> GetAllAsync(string? status, int userId, string role);
    Task<List<IdentityDocumentDto>> GetMineAsync(int customerId);
    Task<IdentityDocumentDto?> GetByIdAsync(int id, int userId, string role);
    Task<IdentityDocumentDto> SubmitAsync(SubmitIdentityDocumentDto dto, int customerId);
    Task<IdentityDocumentDto?> ReviewAsync(int id, ReviewIdentityDocumentDto dto, int reviewerId, string role);
}
