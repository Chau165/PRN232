using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IInvoiceRepository
{
    Task<InvoiceContractDto?> GetContractAsync(int contractId);
    Task<List<BillableServiceDto>> GetBillableServicesAsync(int propertyId, int roomTypeId, DateOnly month);
    Task<MonthUsageDto?> GetUsageAsync(int contractId, DateOnly month);
    Task<bool> ExistsForMonthAsync(int contractId, DateOnly month);
    Task<bool> AddIfAbsentAsync(Invoices invoice, Notifications customerNotification);
    Task<List<InvoiceDto>> GetAllAsync(int? customerId, int? ownerId, int? contractId, string? status);
    Task<InvoiceDto?> GetByIdAsync(int invoiceId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int invoiceId);
}
