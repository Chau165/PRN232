using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IInvoiceComplaintRepository
{
    Task<ComplaintInvoiceReadModel?> GetInvoiceAsync(int invoiceId);
    Task AddAsync(InvoiceComplaints complaint, Notifications ownerNotification);
    Task<List<InvoiceComplaintReadModel>> GetAllAsync(int? customerId, int? ownerId, string? status);
    Task<InvoiceComplaintReadModel?> GetByIdAsync(int complaintId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int complaintId);
    Task UpdateStatusAsync(int complaintId, string status, Notifications customerNotification);
}
