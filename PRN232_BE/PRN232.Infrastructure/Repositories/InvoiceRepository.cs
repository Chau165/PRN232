using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private static readonly SemaphoreSlim SqliteInvoiceWriteGate = new(1, 1);
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
            contract.EndDate,
            contract.BookingId))
            .FirstOrDefaultAsync();
    }

    public Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId)
    {
        return _context.PropertyManagerAssignments.AnyAsync(assignment =>
            assignment.PropertyId == propertyId &&
            assignment.ManagerId == managerId &&
            assignment.Manager!.IsActive &&
            assignment.Manager.Role!.RoleName == RoleNames.Manager);
    }

    public async Task<PrepaidRentCreditReadModel?> GetAvailablePrepaidRentCreditAsync(int contractId)
    {
        var contract = await _context.Contracts
            .AsNoTracking()
            .Where(item => item.ContractId == contractId && item.BookingId != null)
            .Select(item => new
            {
                BookingId = item.BookingId!.Value,
                item.CustomerId,
                DepositAmount = item.Booking!.DepositAmount,
                BookingStatus = item.Booking.Status
            })
            .FirstOrDefaultAsync();

        if (contract is null || contract.BookingStatus != BookingStatuses.Deposited)
        {
            return null;
        }

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(payment => payment.BookingId == contract.BookingId &&
                payment.UserId == contract.CustomerId &&
                payment.Status == PaymentStatuses.Success)
            .OrderBy(payment => payment.PaymentId)
            .Select(payment => payment.Amount)
            .ToListAsync();
        if (payments.Count == 0)
        {
            return null;
        }

        var appliedRows = await _context.InvoiceDetails
            .AsNoTracking()
            .Where(detail => detail.Invoice!.ContractId == contractId &&
                detail.ItemType == InvoiceItemTypes.PrepaidRentCredit &&
                detail.Amount < 0m)
            .Select(detail => detail.Amount)
            .ToListAsync();
        var paidAmount = Math.Min(contract.DepositAmount, payments.Sum());
        var appliedAmount = appliedRows.Sum(amount => -amount);
        var availableAmount = Math.Max(0m, paidAmount - appliedAmount);
        if (availableAmount == 0m)
        {
            return null;
        }

        return new PrepaidRentCreditReadModel(availableAmount);
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

    public async Task<bool> AddIfAbsentAsync(
        Invoices invoice,
        Notifications customerNotification,
        int managerId = 0)
    {
        var usesSqliteGate = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (usesSqliteGate)
        {
            await SqliteInvoiceWriteGate.WaitAsync();
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            if (_context.Database.IsNpgsql())
            {
                // Contract lock serializes duplicate invoice requests and credit application.
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {invoice.ContractId} FOR UPDATE");
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT assignment.\"PropertyId\" FROM \"PropertyManagerAssignments\" assignment JOIN \"Rooms\" room ON room.\"PropertyId\" = assignment.\"PropertyId\" JOIN \"Contracts\" contract ON contract.\"RoomId\" = room.\"RoomId\" WHERE contract.\"ContractId\" = {invoice.ContractId} AND assignment.\"ManagerId\" = {managerId} FOR SHARE OF assignment");
            }

            var contract = await _context.Contracts
                .Where(item => item.ContractId == invoice.ContractId)
                .Select(item => new
                {
                    item.BookingId,
                    item.StartDate,
                    item.Status,
                    PropertyId = item.Room!.PropertyId
                })
                .FirstOrDefaultAsync();
            if (contract is null ||
                contract.Status != ContractStatuses.Active ||
                !await HasActiveManagerAssignmentAsync(contract.PropertyId, managerId) ||
                await ExistsForMonthAsync(invoice.ContractId, invoice.InvoiceMonth))
            {
                return false;
            }

            var creditLines = invoice.InvoiceDetails
                .Where(detail => detail.ItemType == InvoiceItemTypes.PrepaidRentCredit)
                .ToArray();
            var firstContractMonth = new DateOnly(contract.StartDate.Year, contract.StartDate.Month, 1);
            var rentAmount = invoice.InvoiceDetails
                .Where(detail => detail.ItemType == InvoiceItemTypes.Rent)
                .Sum(detail => detail.Amount);
            var availableCredit = invoice.InvoiceMonth == firstContractMonth && contract.BookingId.HasValue
                ? await GetAvailablePrepaidRentCreditAsync(invoice.ContractId)
                : null;
            var permittedCredit = availableCredit is null
                ? 0m
                : Math.Min(availableCredit.AvailableAmount, rentAmount);
            if (creditLines.Length > 1 ||
                (permittedCredit > 0m && creditLines.Length != 1) ||
                (creditLines.Length == 1 &&
                    (creditLines[0].Amount >= 0m || -creditLines[0].Amount != permittedCredit)))
            {
                return false;
            }

            var total = invoice.InvoiceDetails.Sum(detail => detail.Amount);
            if (total <= 0m)
            {
                invoice.Status = InvoiceStatuses.Paid;
            }

            await _context.Invoices.AddAsync(invoice);
            await _context.Notifications.AddAsync(customerNotification);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        finally
        {
            if (usesSqliteGate)
            {
                SqliteInvoiceWriteGate.Release();
            }
        }
    }

    public async Task<List<InvoiceReadModel>> GetAllAsync(int? customerId, int? managerId, int? contractId, string? status)
    {
        var invoices = await Project(InScope(customerId, managerId)
                .Where(invoice => contractId == null || invoice.ContractId == contractId)
                .Where(invoice => status == null || invoice.Status == status)
                .OrderByDescending(invoice => invoice.InvoiceMonth)
             .ThenByDescending(invoice => invoice.InvoiceId))
            .ToListAsync();
        return await WithTotalsAndPaymentsAsync(invoices);
    }

    public async Task<InvoiceReadModel?> GetByIdAsync(int invoiceId, int? customerId, int? managerId)
    {
        var invoice = await Project(InScope(customerId, managerId).Where(item => item.InvoiceId == invoiceId))
            .FirstOrDefaultAsync();
        return invoice is null
            ? null
            : (await WithTotalsAndPaymentsAsync([invoice]))[0];
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
                PropertyId = item.Contract.Room.PropertyId,
                item.Contract.Room.RoomCode,
                item.InvoiceMonth,
                item.Status,
                Amounts = item.InvoiceDetails.Select(detail => detail.Amount).ToList()
            })
            .FirstOrDefaultAsync();
        var paidAmounts = new List<decimal>();
        if (invoice is not null)
        {
            paidAmounts = await _context.Payments
                .AsNoTracking()
                .Where(payment => payment.InvoiceId == invoiceId && payment.Status == PaymentStatuses.Success)
                .Select(payment => payment.Amount)
                .ToListAsync();
        }
        // Tổng tiền cộng trong bộ nhớ, cùng lý do với WithTotal bên dưới.
        return invoice is null
            ? null
            : new InvoicePaymentReadModel(
                invoice.InvoiceId,
                invoice.CustomerId,
                invoice.OwnerId,
                invoice.RoomCode,
                invoice.InvoiceMonth,
                invoice.Status,
                 Math.Max(0m, invoice.Amounts.Sum() - paidAmounts.Sum()),
                 invoice.PropertyId);
    }


    //public async Task<bool> MarkPaidAsync(int invoiceId, Payments payment, Notifications notification, int? ownerId = null)
    //{
    //    await using var transaction = await _context.Database.BeginTransactionAsync();
    //    if (_context.Database.IsNpgsql())
    //        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Invoices\" WHERE \"InvoiceId\" = {invoiceId} FOR UPDATE");
    //    var current = await GetForPaymentAsync(invoiceId);
    //    if (current is null || (ownerId.HasValue && current.OwnerId != ownerId) ||
    //        current.CustomerId != payment.UserId || current.Total != payment.Amount || current.Total <= 0 ||
    //        current.Status is not (InvoiceStatuses.Unpaid or InvoiceStatuses.Overdue)) return false;
    //    if (await _context.PaymentRequests.AnyAsync(request => request.InvoiceId == invoiceId &&
    //        (request.Status == PaymentRequestStatuses.Creating || request.Status == PaymentRequestStatuses.Pending ||
    //         request.Status == PaymentRequestStatuses.RequiresReview))) return false;

    //    // Điều kiện "chưa Paid" nằm ngay trong câu UPDATE: hai lần trả cùng lúc thì lần sau không khớp dòng nào.
    //    var updated = await _context.Invoices
    //        .Where(invoice => invoice.InvoiceId == invoiceId && invoice.Status != InvoiceStatuses.Paid)
    //        .ExecuteUpdateAsync(setters => setters.SetProperty(invoice => invoice.Status, InvoiceStatuses.Paid));
    //    if (updated == 0)
    //    {
    //        return false;
    //    }

    //    await _context.Payments.AddAsync(payment);
    //    await _context.Notifications.AddAsync(notification);
    //    await _context.SaveChangesAsync();
    //    await transaction.CommitAsync();
    //    return true;
    //}
    public async Task<InvoicePaymentAttemptStatus> TryPayOnlineAsync(
        int invoiceId,
        int customerId,
        string paymentMethod,
        Func<decimal, Task<bool>> charge,
        Notifications notification,
        DateTime? paidAtUtc = null)
    {
        var usesSqliteGate = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (usesSqliteGate)

        {
            await SqliteInvoiceWriteGate.WaitAsync();
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            if (_context.Database.IsNpgsql())
            {
                // Hold the invoice row lock across the gateway call so a duplicate request cannot charge again.
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Invoices\" WHERE \"InvoiceId\" = {invoiceId} FOR UPDATE");
            }

            var invoice = await GetForPaymentAsync(invoiceId);
            if (invoice is null)
            {
                return InvoicePaymentAttemptStatus.NotFound;
            }
            if (invoice.CustomerId != customerId)
            {
                return InvoicePaymentAttemptStatus.Forbidden;
            }
            if (invoice.Status == InvoiceStatuses.Paid)
            {
                return InvoicePaymentAttemptStatus.Conflict;
            }

            if (invoice.Total <= 0m)
            {
                var settled = await _context.Invoices
                    .Where(item => item.InvoiceId == invoiceId && item.Status != InvoiceStatuses.Paid)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, InvoiceStatuses.Paid));
                if (settled == 0)
                {
                    return InvoicePaymentAttemptStatus.Conflict;
                }

                await transaction.CommitAsync();
                return InvoicePaymentAttemptStatus.NoAmountDue;
            }

            if (!await charge(invoice.Total))
            {
                return InvoicePaymentAttemptStatus.Declined;
            }

            var updated = await _context.Invoices
                .Where(item => item.InvoiceId == invoiceId && item.Status != InvoiceStatuses.Paid)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, InvoiceStatuses.Paid));
            if (updated == 0)
            {
                return InvoicePaymentAttemptStatus.Conflict;
            }

            await _context.Payments.AddAsync(new Payments
            {
                UserId = customerId,
                InvoiceId = invoiceId,
                Amount = invoice.Total,
                PaidAtUtc = paidAtUtc ?? DateTime.UtcNow,
                PaymentMethod = paymentMethod,
                Status = PaymentStatuses.Success
            });
            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return InvoicePaymentAttemptStatus.Completed;
        }
        finally
        {
            if (usesSqliteGate)
            {
                SqliteInvoiceWriteGate.Release();
            }
        }
    }

    public async Task<bool> MarkPaidAsync(
        int invoiceId,
        Payments payment,
        Notifications notification,
        int? managerId = null)
    {
        var usesSqliteGate = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (usesSqliteGate)
        {
            await SqliteInvoiceWriteGate.WaitAsync();
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            if (managerId.HasValue)
            {
                if (_context.Database.IsNpgsql())
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM \"Invoices\" WHERE \"InvoiceId\" = {invoiceId} FOR UPDATE");
                }

                var propertyId = await _context.Invoices
                    .Where(invoice => invoice.InvoiceId == invoiceId)
                    .Select(invoice => (int?)invoice.Contract!.Room!.PropertyId)
                    .FirstOrDefaultAsync();
                if (!propertyId.HasValue)
                {
                    return false;
                }

                if (_context.Database.IsNpgsql())
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT assignment.\"PropertyId\" FROM \"PropertyManagerAssignments\" assignment WHERE assignment.\"PropertyId\" = {propertyId.Value} AND assignment.\"ManagerId\" = {managerId.Value} FOR SHARE");
                }

                if (!await HasActiveManagerAssignmentAsync(propertyId.Value, managerId.Value))
                {
                    return false;
                }
            }

            var current = await GetForPaymentAsync(invoiceId);
            if (current is null || current.CustomerId != payment.UserId || payment.InvoiceId != invoiceId ||
                current.Total != payment.Amount || current.Total < 0m ||
                current.Status is not (InvoiceStatuses.Unpaid or InvoiceStatuses.Overdue)) return false;
            if (await _context.PaymentRequests.AnyAsync(request => request.InvoiceId == invoiceId &&
                (request.Status == PaymentRequestStatuses.Creating || request.Status == PaymentRequestStatuses.Pending ||
                 request.Status == PaymentRequestStatuses.RequiresReview))) return false;

            // The condition lives in UPDATE: only one cash or other status transition can win.
            var updated = await _context.Invoices
                .Where(invoice => invoice.InvoiceId == invoiceId && invoice.Status != InvoiceStatuses.Paid)
                .ExecuteUpdateAsync(setters => setters.SetProperty(invoice => invoice.Status, InvoiceStatuses.Paid));
            if (updated == 0)
            {
                return false;
            }

            if (payment.Amount > 0m)
            {
                await _context.Payments.AddAsync(payment);
            }
            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        finally
        {
            if (usesSqliteGate)
            {
                SqliteInvoiceWriteGate.Release();
            }
        }
    }

    // Lọc dùng chung cho GetById và GetAll; tham số nào null thì không lọc theo tiêu chí đó.
    private IQueryable<Invoices> InScope(int? customerId, int? managerId)
    {
        return _context.Invoices
            .AsNoTracking()
            .Where(invoice => customerId == null || invoice.Contract!.CustomerId == customerId)
            .Where(invoice => managerId == null || invoice.Contract!.Room!.Property!.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == RoleNames.Manager));
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
            .ToList(),
            0m,
            0m,
            null,
            null));
    }

    private Task<List<InvoiceReadModel>> WithTotalsAndPaymentsAsync(List<InvoiceReadModel> invoices)
    {
        // Shared with the contract timeline so totals, credit, and payments stay consistent.
        return InvoiceReadModelEnricher.WithTotalsAndPaymentsAsync(_context, invoices);
    }

}
