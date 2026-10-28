using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IInvoiceService
{
    Task<InvoiceDto> PreviewAsync(CreateInvoiceDto dto, int userId, string role);
    Task<InvoiceDto> CreateAsync(CreateInvoiceDto dto, int userId, string role);
    Task<List<InvoiceDto>> GetAllAsync(int userId, string role, int? contractId, string? status);
    Task<InvoiceDto?> GetByIdAsync(int id, int userId, string role);
}
