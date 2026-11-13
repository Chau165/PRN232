using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Application.ReadModels;
using PRN232.Application.States;

namespace PRN232.Infrastructure.Repositories;

public class IdentityDocumentRepository : IIdentityDocumentRepository
{
    private readonly AppDbContext _context;

    public IdentityDocumentRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<IdentityDocumentReadModel>> GetAllAsync(string? status, int? managerId)
    {
        var documents = _context.IdentityDocuments
            .AsNoTracking()
            .Where(d => status == null || d.Status == status);
        if (managerId.HasValue)
        {
            var customerIds = CustomerIdsOfManager(managerId.Value);
            documents = documents.Where(d => customerIds.Contains(d.CustomerId));
        }

        return documents
            .OrderBy(d => d.IdentityDocumentId)
            .Select(d => new IdentityDocumentReadModel(
            d.IdentityDocumentId,
            d.CustomerId,
            d.Customer!.FullName,
            d.CCCDNumber,
            d.Status,
            d.ReviewedBy,
            d.ReviewedByUser == null ? null : d.ReviewedByUser.FullName))
            .ToListAsync();
    }

    public Task<List<IdentityDocumentReadModel>> GetByCustomerIdAsync(int customerId)
    {
        return _context.IdentityDocuments
            .AsNoTracking()
            .Where(d => d.CustomerId == customerId)
            .OrderBy(d => d.IdentityDocumentId)
            .Select(d => new IdentityDocumentReadModel(
            d.IdentityDocumentId,
            d.CustomerId,
            d.Customer!.FullName,
            d.CCCDNumber,
            d.Status,
            d.ReviewedBy,
            d.ReviewedByUser == null ? null : d.ReviewedByUser.FullName))
            .ToListAsync();
    }

    public Task<IdentityDocumentReadModel?> GetByIdAsync(int id)
    {
        return _context.IdentityDocuments
            .AsNoTracking()
            .Where(d => d.IdentityDocumentId == id)
            .Select(d => new IdentityDocumentReadModel(
            d.IdentityDocumentId,
            d.CustomerId,
            d.Customer!.FullName,
            d.CCCDNumber,
            d.Status,
            d.ReviewedBy,
            d.ReviewedByUser == null ? null : d.ReviewedByUser.FullName))
            .FirstOrDefaultAsync();
    }

    public Task<IdentityDocuments?> GetTrackedByIdAsync(int id)
    {
        return _context.IdentityDocuments.FirstOrDefaultAsync(d => d.IdentityDocumentId == id);
    }

    public Task<bool> IsCustomerOfManagerAsync(int customerId, int managerId)
    {
        return CustomerIdsOfManager(managerId).AnyAsync(id => id == customerId);
    }

    // A3: a customer belongs to a Manager when they have a non-cancelled booking or a contract on a
    // room whose property is assigned to that Manager in PropertyManagerAssignments. Property.OwnerId
    // is deliberately not used: owning a property does not make someone its Manager.
    private IQueryable<int> CustomerIdsOfManager(int managerId)
    {
        var bookingCustomers = _context.Bookings
            .Where(b => b.Status != BookingStatuses.Cancelled &&
                b.Room!.Property!.ManagerAssignments.Any(a => a.ManagerId == managerId))
            .Select(b => b.CustomerId);
        var contractCustomers = _context.Contracts
            .Where(c => c.Room!.Property!.ManagerAssignments.Any(a => a.ManagerId == managerId))
            .Select(c => c.CustomerId);
        return bookingCustomers.Concat(contractCustomers);
    }

    public Task<bool> HasPendingOrApprovedAsync(int customerId)
    {
        return _context.IdentityDocuments.AnyAsync(d =>
            d.CustomerId == customerId &&
            (d.Status == "Pending" || d.Status == "Approved"));
    }

    public Task<bool> CccdNumberUsedByOtherAsync(string cccdNumberHash, int customerId)
    {
        return _context.IdentityDocuments.AnyAsync(d =>
            d.CCCDNumberHash == cccdNumberHash && d.CustomerId != customerId);
    }

    public async Task<IdentityDocuments> AddAsync(IdentityDocuments document)
    {
        await _context.IdentityDocuments.AddAsync(document);
        return document;
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
