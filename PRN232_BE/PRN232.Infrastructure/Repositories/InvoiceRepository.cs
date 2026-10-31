using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly AppDbContext _context;

    public InvoiceRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<InvoiceContractReadModel?> GetContractAsync(int contractId)
    {
        return _context.Contracts
            .AsNoTracking()
            .Where(contract => contract.ContractId == contractId)
            .Select(contract => new InvoiceContractReadModel(
            contract.ContractId,
            contract.Room!.Property!.OwnerId,
            contract.CustomerId,
            contract.Customer!.FullName,
            contract.Room.RoomCode,
            contract.Room.PropertyId,
            contract.Room.Property.PropertyName,
            contract.Room.RoomTypeId,
            contract.MonthlyRent,
            contract.Status,
            contract.StartDate,
            contract.EndDate))
            .FirstOrDefaultAsync();
    }

    // Dịch vụ lấy theo loại phòng (giống danh sách dịch vụ trên trang phòng), giá lấy theo khu trọ
    // với cùng điều kiện hiệu lực mà trang công khai dùng. Ghép trong bộ nhớ để chạy được cả trên SQLite.
    public async Task<List<BillableServiceReadModel>> GetBillableServicesAsync(int propertyId, int roomTypeId, DateOnly month)
    {
        var services = await _context.RoomTypeServices
            .AsNoTracking()
            .Where(link => link.RoomTypeId == roomTypeId)
            .OrderBy(link => link.ServiceId)
            .Select(link => new { link.ServiceId, link.Service!.Name })
            .ToListAsync();
        var rates = await _context.PropertyServiceRates
            .AsNoTracking()
            .Where(rate => rate.PropertyId == propertyId &&
                rate.EffectiveFrom <= month &&
                (rate.EffectiveTo == null || rate.EffectiveTo > month))
            .Select(rate => new { rate.ServiceId, rate.RateId, rate.Price, rate.UnitCode, rate.BillingBasis, rate.EffectiveFrom })
            .ToListAsync();

        return services
            .Select(service =>
            {
                var rate = rates
                    .Where(item => item.ServiceId == service.ServiceId)
                    .OrderByDescending(item => item.EffectiveFrom)
                    .FirstOrDefault();
                return new BillableServiceReadModel(
                    service.ServiceId, service.Name, rate?.RateId, rate?.Price, rate?.UnitCode, rate?.BillingBasis);
            })
            .ToList();
    }

    public async Task<MonthUsageReadModel?> GetUsageAsync(int contractId, DateOnly month)
    {
        var reading = await _context.UtilityReadings
            .AsNoTracking()
            .Where(item => item.ContractId == contractId && item.ReadingMonth == month)
            .Select(item => new { item.ElectricStart, item.ElectricEnd, item.WaterStart, item.WaterEnd })
            .FirstOrDefaultAsync();
        return reading is null
            ? null
            : new MonthUsageReadModel(reading.ElectricEnd - reading.ElectricStart, reading.WaterEnd - reading.WaterStart);
    }

    public Task<bool> ExistsForMonthAsync(int contractId, DateOnly month)
    {
        return _context.Invoices.AnyAsync(invoice => invoice.ContractId == contractId && invoice.InvoiceMonth == month);
    }

    public async Task<bool> AddIfAbsentAsync(Invoices invoice, Notifications customerNotification)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (_context.Database.IsNpgsql())
        {
            // Khoá hợp đồng để hai lần bấm "Tạo hoá đơn" cùng lúc không tạo ra hai hoá đơn cho một tháng.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {invoice.ContractId} FOR UPDATE");
        }

        if (await ExistsForMonthAsync(invoice.ContractId, invoice.InvoiceMonth))
        {
            return false;
        }

        await _context.Invoices.AddAsync(invoice);
        await _context.Notifications.AddAsync(customerNotification);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<List<InvoiceReadModel>> GetAllAsync(int? customerId, int? ownerId, int? contractId, string? status)
    {
        var invoices = await Project(InScope(customerId, ownerId)
                .Where(invoice => contractId == null || invoice.ContractId == contractId)
                .Where(invoice => status == null || invoice.Status == status)
                .OrderByDescending(invoice => invoice.InvoiceMonth)
                .ThenByDescending(invoice => invoice.InvoiceId))
            .ToListAsync();
        return invoices.Select(WithTotal).ToList();
    }

    public async Task<InvoiceReadModel?> GetByIdAsync(int invoiceId, int? customerId, int? ownerId)
    {
        var invoice = await Project(InScope(customerId, ownerId).Where(item => item.InvoiceId == invoiceId))
            .FirstOrDefaultAsync();
        return invoice is null ? null : WithTotal(invoice);
    }

    public Task<bool> ExistsAsync(int invoiceId)
    {
        return _context.Invoices.AnyAsync(invoice => invoice.InvoiceId == invoiceId);
    }

    public async Task<InvoicePaymentReadModel?> GetForPaymentAsync(int invoiceId)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId)
            .Select(item => new
            {
                item.InvoiceId,
                item.Contract!.CustomerId,
                item.Contract.Room!.Property!.OwnerId,
                item.Contract.Room.RoomCode,
                item.InvoiceMonth,
                item.Status,
                Amounts = item.InvoiceDetails.Select(detail => detail.Amount).ToList()
            })
            .FirstOrDefaultAsync();
        // Tổng tiền cộng trong bộ nhớ, cùng lý do với WithTotal bên dưới.
        return invoice is null
            ? null
            : new InvoicePaymentReadModel(invoice.InvoiceId, invoice.CustomerId, invoice.OwnerId, invoice.RoomCode,
                invoice.InvoiceMonth, invoice.Status, invoice.Amounts.Sum());
    }

    public async Task<bool> MarkPaidAsync(int invoiceId, Payments payment, Notifications notification)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Điều kiện "chưa Paid" nằm ngay trong câu UPDATE: hai lần trả cùng lúc thì lần sau không khớp dòng nào.
        var updated = await _context.Invoices
            .Where(invoice => invoice.InvoiceId == invoiceId && invoice.Status != InvoiceStatuses.Paid)
            .ExecuteUpdateAsync(setters => setters.SetProperty(invoice => invoice.Status, InvoiceStatuses.Paid));
        if (updated == 0)
        {
            return false;
        }

        await _context.Payments.AddAsync(payment);
        await _context.Notifications.AddAsync(notification);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    // Lọc dùng chung cho GetById và GetAll; tham số nào null thì không lọc theo tiêu chí đó.
    private IQueryable<Invoices> InScope(int? customerId, int? ownerId)
    {
        return _context.Invoices
            .AsNoTracking()
            .Where(invoice => customerId == null || invoice.Contract!.CustomerId == customerId)
            .Where(invoice => ownerId == null || invoice.Contract!.Room!.Property!.OwnerId == ownerId);
    }

    private static IQueryable<InvoiceReadModel> Project(IQueryable<Invoices> invoices)
    {
        return invoices.Select(invoice => new InvoiceReadModel(
            invoice.InvoiceId,
            invoice.ContractId,
            invoice.Contract!.Room!.RoomCode,
            invoice.Contract.Room.Property!.PropertyName,
            invoice.Contract.Customer!.FullName,
            invoice.InvoiceMonth,
            invoice.DueDate,
            invoice.Status,
            0m,
            invoice.InvoiceDetails
            .OrderBy(detail => detail.InvoiceDetailId)
            .Select(detail => new InvoiceLineReadModel(
            detail.ItemType, detail.Description, detail.Quantity, detail.UnitPrice, detail.UnitCode, detail.Amount))
            .ToList()));
    }

    // Tổng tiền cộng trong bộ nhớ: bảng Invoices không có cột tổng, và SQLite không cộng được kiểu decimal.
    private static InvoiceReadModel WithTotal(InvoiceReadModel invoice)
    {
        return invoice with
        {
            Total = invoice.Lines.Sum(line => line.Amount)
        };
    }
}
