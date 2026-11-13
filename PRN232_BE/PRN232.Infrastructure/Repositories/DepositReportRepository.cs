using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.States;
using PRN232.Infrastructure.Data;
namespace PRN232.Infrastructure.Repositories;

public class DepositReportRepository(AppDbContext context) : IDepositReportRepository
{
    public async Task<IReadOnlyList<DepositReportRow>> ReadAsync(int? propertyId, int? managerId, DateTime fromUtc, DateTime untilUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            context.Database.IsNpgsql() ? System.Data.IsolationLevel.RepeatableRead : System.Data.IsolationLevel.Serializable, cancellationToken);
        var properties = context.Properties.AsNoTracking()
            .Where(p => propertyId == null || p.PropertyId == propertyId)
            .Where(p => managerId == null || p.ManagerAssignments.Any(a => a.ManagerId == managerId && a.Manager!.IsActive && a.Manager.Role!.RoleName == RoleNames.Manager));
        // Independent receipt and refund queries: never join detail lines, requests or payout retries to sum money.
        var receipts = await (from p in context.Payments.AsNoTracking()
            join b in context.Bookings on p.BookingId equals b.BookingId
            join r in context.Rooms on b.RoomId equals r.RoomId
            join property in properties on r.PropertyId equals property.PropertyId
            where p.Status == "Success" && p.InvoiceId == null && p.ContractId == null && p.ReadingId == null
                && (p.PaidAtUtc == null || p.PaidAtUtc >= fromUtc && p.PaidAtUtc < untilUtc)
            select new DepositReportRow(property.PropertyId, property.PropertyName, p.PaidAtUtc, p.Amount, 0m)).ToListAsync(cancellationToken);
        var refunds = await (from refund in context.BookingRefunds.AsNoTracking()
            join b in context.Bookings on refund.BookingId equals b.BookingId
            join r in context.Rooms on b.RoomId equals r.RoomId
            join property in properties on r.PropertyId equals property.PropertyId
            where refund.Currency == "VND" && (refund.TransferredAtUtc == null || refund.TransferredAtUtc >= fromUtc && refund.TransferredAtUtc < untilUtc)
            select new DepositReportRow(property.PropertyId, property.PropertyName, refund.TransferredAtUtc, 0m, refund.Amount)).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return receipts.Concat(refunds).ToList();
    }
}
