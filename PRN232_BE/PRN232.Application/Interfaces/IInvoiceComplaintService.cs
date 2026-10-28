using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IInvoiceComplaintService
{
    Task<InvoiceComplaintDto> CreateAsync(CreateInvoiceComplaintDto dto, int customerId);
    Task<List<InvoiceComplaintDto>> GetAllAsync(int userId, string role, string? status);
    Task<InvoiceComplaintDto?> GetByIdAsync(int id, int userId, string role);
    Task<InvoiceComplaintDto> UpdateStatusAsync(int id, UpdateComplaintStatusDto dto, int userId, string role);
}
