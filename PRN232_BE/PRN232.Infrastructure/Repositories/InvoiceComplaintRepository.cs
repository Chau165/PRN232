using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class InvoiceComplaintRepository : IInvoiceComplaintRepository
{
    private readonly AppDbContext _context;

    public InvoiceComplaintRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ComplaintInvoiceReadModel?> GetInvoiceAsync(int invoiceId)
    {
        return _context.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.InvoiceId == invoiceId)
            .Select(invoice => new ComplaintInvoiceReadModel(
            invoice.InvoiceId,
            invoice.Contract!.CustomerId,
            invoice.Contract.Room!.Property!.OwnerId,
            invoice.Contract.Room.RoomCode,
            invoice.InvoiceMonth,
            invoice.InvoiceDetails.Select(detail => detail.Description).ToList()))
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(InvoiceComplaints complaint, Notifications ownerNotification)
    {
        await _context.InvoiceComplaints.AddAsync(complaint);
        await _context.Notifications.AddAsync(ownerNotification);
        await _context.SaveChangesAsync();
    }

    public Task<List<InvoiceComplaintReadModel>> GetAllAsync(int? customerId, int? ownerId, string? status)
    {
        return InScope(customerId, ownerId)
            .Where(complaint => status == null || complaint.Status == status)
            .OrderByDescending(complaint => complaint.ComplaintId)
            .Select(ToReadModel)
            .ToListAsync();
    }

    public Task<InvoiceComplaintReadModel?> GetByIdAsync(int complaintId, int? customerId, int? ownerId)
    {
        return InScope(customerId, ownerId)
            .Where(complaint => complaint.ComplaintId == complaintId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync();
    }

    public Task<bool> ExistsAsync(int complaintId)
    {
        return _context.InvoiceComplaints.AnyAsync(complaint => complaint.ComplaintId == complaintId);
    }

    public async Task UpdateStatusAsync(int complaintId, string status, Notifications customerNotification)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.InvoiceComplaints
            .Where(complaint => complaint.ComplaintId == complaintId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(complaint => complaint.Status, status));
        await _context.Notifications.AddAsync(customerNotification);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    // Lọc dùng chung cho GetById và GetAll; tham số nào null thì không lọc theo tiêu chí đó.
    private IQueryable<InvoiceComplaints> InScope(int? customerId, int? ownerId)
    {
        return _context.InvoiceComplaints
            .AsNoTracking()
            .Where(complaint => customerId == null || complaint.CustomerId == customerId)
            .Where(complaint => ownerId == null || complaint.Invoice!.Contract!.Room!.Property!.OwnerId == ownerId);
    }

    private static readonly Expression<Func<InvoiceComplaints, InvoiceComplaintReadModel>> ToReadModel = complaint => new InvoiceComplaintReadModel(
        complaint.ComplaintId,
        complaint.InvoiceId,
        complaint.Invoice!.InvoiceMonth,
        complaint.Invoice.Contract!.Room!.RoomCode,
        complaint.Invoice.Contract.Room.Property!.PropertyName,
        complaint.CustomerId,
        complaint.Customer!.FullName,
        complaint.FeeType,
        complaint.Content,
        complaint.Status,
        complaint.CreatedAt);
}
