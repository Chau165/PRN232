using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class ReceivablesRepository(AppDbContext context) : IReceivablesRepository
{
    public Task<int?> GetPropertyOwnerAsync(int propertyId, CancellationToken cancellationToken) =>
        context.Properties.AsNoTracking().Where(property => property.PropertyId == propertyId)
            .Select(property => (int?)property.OwnerId).FirstOrDefaultAsync(cancellationToken);

    public async Task<List<ReceivableReadModel>> GetOutstandingAsync(
        int? propertyId, int? ownerId, CancellationToken cancellationToken)
    {
        // Scope in SQL before reading amounts. One query avoids mixing invoice/line snapshots.
        // Sum decimals in memory, as the existing invoice reader does, for SQLite compatibility.
        var invoices = await context.Invoices.AsNoTracking().AsSingleQuery()
            .Where(invoice => invoice.Status == InvoiceStatuses.Unpaid || invoice.Status == InvoiceStatuses.Overdue)
            .Where(invoice => propertyId == null || invoice.Contract!.Room!.PropertyId == propertyId)
            .Where(invoice => ownerId == null || invoice.Contract!.Room!.Property!.OwnerId == ownerId)
            .Select(invoice => new
            {
                invoice.DueDate,
                Amounts = invoice.InvoiceDetails.Select(line => line.Amount).ToList()
            }).ToListAsync(cancellationToken);
        return invoices.Select(invoice => new ReceivableReadModel(invoice.DueDate, invoice.Amounts.Sum())).ToList();
    }
}
