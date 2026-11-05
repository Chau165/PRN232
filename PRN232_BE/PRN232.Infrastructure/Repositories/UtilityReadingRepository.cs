using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class UtilityReadingRepository : IUtilityReadingRepository
{
    private readonly AppDbContext _context;

    public UtilityReadingRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ReadingContractReadModel?> GetContractAsync(int contractId)
    {
        return _context.Contracts
            .AsNoTracking()
            .Where(contract => contract.ContractId == contractId)
            .Select(contract => new ReadingContractReadModel(
            contract.ContractId,
            contract.Room!.Property!.OwnerId,
            contract.Status,
            contract.StartDate,
            contract.EndDate,
            contract.Room!.PropertyId))
            .FirstOrDefaultAsync();
    }

    public Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId)
    {
        return _context.PropertyManagerAssignments.AnyAsync(assignment =>
            assignment.PropertyId == propertyId &&
            assignment.ManagerId == managerId &&
            assignment.Manager!.IsActive &&
            assignment.Manager.Role!.RoleName == PRN232.Application.States.RoleNames.Manager);
    }

    public Task<LatestReadingReadModel?> GetLatestAsync(int contractId)
    {
        return _context.UtilityReadings
            .AsNoTracking()
            .Where(reading => reading.ContractId == contractId)
            .OrderByDescending(reading => reading.ReadingMonth)
            .Select(reading => new LatestReadingReadModel(reading.ReadingMonth, reading.ElectricEnd, reading.WaterEnd))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> AddIfLatestAsync(UtilityReadings reading, int managerId = 0)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (_context.Database.IsNpgsql())
        {
            // Khoá hợp đồng: hai người nhập chỉ số cùng lúc cho một hợp đồng sẽ phải xếp hàng,
            // người sau kiểm tra lại sẽ thấy tháng vừa ghi và bị từ chối.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {reading.ContractId} FOR UPDATE");
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT assignment.\"PropertyId\" FROM \"PropertyManagerAssignments\" assignment JOIN \"Rooms\" room ON room.\"PropertyId\" = assignment.\"PropertyId\" JOIN \"Contracts\" contract ON contract.\"RoomId\" = room.\"RoomId\" WHERE contract.\"ContractId\" = {reading.ContractId} AND assignment.\"ManagerId\" = {managerId} FOR SHARE OF assignment");
        }

        var propertyId = await _context.Contracts
            .Where(contract => contract.ContractId == reading.ContractId)
            .Select(contract => (int?)contract.Room!.PropertyId)
            .FirstOrDefaultAsync();
        if (!propertyId.HasValue || !await HasActiveManagerAssignmentAsync(propertyId.Value, managerId))
        {
            return false;
        }

        var monthTaken = await _context.UtilityReadings.AnyAsync(existing =>
            existing.ContractId == reading.ContractId && existing.ReadingMonth >= reading.ReadingMonth);
        if (monthTaken)
        {
            return false;
        }

        await _context.UtilityReadings.AddAsync(reading);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public Task<List<UtilityReadingReadModel>> GetAllAsync(int? customerId, int? managerId, int? contractId)
    {
        return InScope(customerId, managerId)
            .Where(reading => contractId == null || reading.ContractId == contractId)
            .OrderByDescending(reading => reading.ReadingMonth)
            .ThenByDescending(reading => reading.ReadingId)
            .Select(ToReadModel)
            .ToListAsync();
    }

    public Task<UtilityReadingReadModel?> GetByIdAsync(int readingId, int? customerId, int? managerId)
    {
        return InScope(customerId, managerId)
            .Where(reading => reading.ReadingId == readingId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync();
    }

    public Task<bool> ExistsAsync(int readingId)
    {
        return _context.UtilityReadings.AnyAsync(reading => reading.ReadingId == readingId);
    }

    // Lọc dùng chung cho GetById và GetAll; tham số nào null thì không lọc theo tiêu chí đó.
    private IQueryable<UtilityReadings> InScope(int? customerId, int? managerId)
    {
        return _context.UtilityReadings
            .AsNoTracking()
            .Where(reading => customerId == null || reading.Contract!.CustomerId == customerId)
            .Where(reading => managerId == null || reading.Contract!.Room!.Property!.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == PRN232.Application.States.RoleNames.Manager));
    }

    private static readonly Expression<Func<UtilityReadings, UtilityReadingReadModel>> ToReadModel = reading => new UtilityReadingReadModel(
        reading.ReadingId,
        reading.ContractId,
        reading.Contract!.Room!.RoomCode,
        reading.Contract.Room.Property!.PropertyName,
        reading.Contract.Customer!.FullName,
        reading.ReadingMonth,
        reading.ElectricStart,
        reading.ElectricEnd,
        reading.ElectricEnd - reading.ElectricStart,
        reading.WaterStart,
        reading.WaterEnd,
        reading.WaterEnd - reading.WaterStart);
}
