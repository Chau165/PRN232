using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IInvoiceRepository
{
    Task<InvoiceContractReadModel?> GetContractAsync(int contractId);
    Task<List<BillableServiceReadModel>> GetBillableServicesAsync(int propertyId, int roomTypeId, DateOnly month);
    Task<MonthUsageReadModel?> GetUsageAsync(int contractId, DateOnly month);
    Task<bool> ExistsForMonthAsync(int contractId, DateOnly month);
    Task<bool> AddIfAbsentAsync(Invoices invoice, Notifications customerNotification);
    Task<List<InvoiceReadModel>> GetAllAsync(int? customerId, int? ownerId, int? contractId, string? status);
    Task<InvoiceReadModel?> GetByIdAsync(int invoiceId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int invoiceId);
    Task<InvoicePaymentReadModel?> GetForPaymentAsync(int invoiceId);
    Task<bool> MarkPaidAsync(int invoiceId, Payments payment, Notifications notification);
}
