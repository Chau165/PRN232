using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Application.ReadModels;

namespace PRN232.Infrastructure.Repositories;

public class IdentityDocumentRepository : IIdentityDocumentRepository
{
    private readonly AppDbContext _context;

    public IdentityDocumentRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<IdentityDocumentReadModel>> GetAllAsync(string? status) =>
        _context.IdentityDocuments
            .AsNoTracking()
            .Where(d => status == null || d.Status == status)
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

    public Task<List<IdentityDocumentReadModel>> GetByCustomerIdAsync(int customerId) =>
        _context.IdentityDocuments
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

    public Task<IdentityDocumentReadModel?> GetByIdAsync(int id) =>
        _context.IdentityDocuments
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

    public Task<IdentityDocuments?> GetTrackedByIdAsync(int id) =>
        _context.IdentityDocuments.FirstOrDefaultAsync(d => d.IdentityDocumentId == id);

    public Task<bool> HasPendingOrApprovedAsync(int customerId) =>
        _context.IdentityDocuments.AnyAsync(d =>
            d.CustomerId == customerId &&
            (d.Status == "Pending" || d.Status == "Approved"));

    public Task<bool> CccdNumberUsedByOtherAsync(string cccdNumber, int customerId) =>
        _context.IdentityDocuments.AnyAsync(d =>
            d.CCCDNumber == cccdNumber && d.CustomerId != customerId);

    public async Task<IdentityDocuments> AddAsync(IdentityDocuments document)
    {
        await _context.IdentityDocuments.AddAsync(document);
        return document;
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
