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

    public Task<List<InvoiceComplaintReadModel>> GetAllAsync(int? customerId, int? managerId, string? status)
    {
        return InScope(customerId, managerId)
            .Where(complaint => status == null || complaint.Status == status)
            .OrderByDescending(complaint => complaint.ComplaintId)
            .Select(ToReadModel)
            .ToListAsync();
    }

    public Task<InvoiceComplaintReadModel?> GetByIdAsync(int complaintId, int? customerId, int? managerId)
    {
        return InScope(customerId, managerId)
            .Where(complaint => complaint.ComplaintId == complaintId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync();
    }

    public Task<bool> ExistsAsync(int complaintId)
    {
        return _context.InvoiceComplaints.AnyAsync(complaint => complaint.ComplaintId == complaintId);
    }

    public Task<bool> HasActiveManagerAssignmentAsync(int complaintId, int managerId)
    {
        return _context.InvoiceComplaints.AnyAsync(complaint =>
            complaint.ComplaintId == complaintId &&
            complaint.Invoice!.Contract!.Room!.Property!.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == PRN232.Application.States.RoleNames.Manager));
    }

    public async Task<bool> UpdateStatusAsync(
        int complaintId,
        string status,
        Notifications customerNotification,
        int? managerId = null)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (managerId.HasValue)
        {
            var propertyId = await _context.InvoiceComplaints
                .Where(complaint => complaint.ComplaintId == complaintId)
                .Select(complaint => (int?)complaint.Invoice!.Contract!.Room!.PropertyId)
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

            if (!await _context.PropertyManagerAssignments.AnyAsync(assignment =>
                    assignment.PropertyId == propertyId.Value &&
                    assignment.ManagerId == managerId.Value &&
                    assignment.Manager!.IsActive &&
                    assignment.Manager.Role!.RoleName == PRN232.Application.States.RoleNames.Manager))
            {
                return false;
            }
        }

        var updated = await _context.InvoiceComplaints
            .Where(complaint => complaint.ComplaintId == complaintId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(complaint => complaint.Status, status));
        if (updated == 0)
        {
            return false;
        }

        await _context.Notifications.AddAsync(customerNotification);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    // Lọc dùng chung cho GetById và GetAll; tham số nào null thì không lọc theo tiêu chí đó.
    private IQueryable<InvoiceComplaints> InScope(int? customerId, int? managerId)
    {
        return _context.InvoiceComplaints
            .AsNoTracking()
            .Where(complaint => customerId == null || complaint.CustomerId == customerId)
            .Where(complaint => managerId == null || complaint.Invoice!.Contract!.Room!.Property!.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == PRN232.Application.States.RoleNames.Manager));
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
