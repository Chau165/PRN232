using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IInvoiceRepository
{
    Task<InvoiceContractReadModel?> GetContractAsync(int contractId);
    Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId);
    Task<PrepaidRentCreditReadModel?> GetAvailablePrepaidRentCreditAsync(int contractId);
    Task<List<BillableServiceReadModel>> GetBillableServicesAsync(int propertyId, int roomTypeId, DateOnly month);
    Task<MonthUsageReadModel?> GetUsageAsync(int contractId, DateOnly month);
    Task<bool> ExistsForMonthAsync(int contractId, DateOnly month);
    Task<bool> AddIfAbsentAsync(Invoices invoice, Notifications customerNotification, int managerId = 0);
    Task<List<InvoiceReadModel>> GetAllAsync(int? customerId, int? ownerId, int? contractId, string? status);
    Task<InvoiceReadModel?> GetByIdAsync(int invoiceId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int invoiceId);
    Task<InvoicePaymentReadModel?> GetForPaymentAsync(int invoiceId);
    Task<InvoicePaymentAttemptStatus> TryPayOnlineAsync(
        int invoiceId,
        int customerId,
        string paymentMethod,
        Func<decimal, Task<bool>> charge,
        Notifications notification,
        DateTime? paidAtUtc = null);
    Task<bool> MarkPaidAsync(
        int invoiceId,
        Payments payment,
        Notifications notification,
        int? managerId = null);

}
