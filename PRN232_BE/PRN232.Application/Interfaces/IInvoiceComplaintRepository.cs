using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IInvoiceComplaintRepository
{
    Task<ComplaintInvoiceDto?> GetInvoiceAsync(int invoiceId);
    Task AddAsync(InvoiceComplaints complaint, Notifications ownerNotification);
    Task<List<InvoiceComplaintDto>> GetAllAsync(int? customerId, int? ownerId, string? status);
    Task<InvoiceComplaintDto?> GetByIdAsync(int complaintId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int complaintId);
    Task UpdateStatusAsync(int complaintId, string status, Notifications customerNotification);
}
