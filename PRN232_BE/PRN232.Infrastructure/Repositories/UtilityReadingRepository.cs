using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
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

    public Task<ReadingContractDto?> GetContractAsync(int contractId) =>
        _context.Contracts
            .AsNoTracking()
            .Where(contract => contract.ContractId == contractId)
            .Select(contract => new ReadingContractDto(
                contract.ContractId,
                contract.Room!.Property!.OwnerId,
                contract.Status,
                contract.StartDate,
                contract.EndDate))
            .FirstOrDefaultAsync();

    public Task<LatestReadingDto?> GetLatestAsync(int contractId) =>
        _context.UtilityReadings
            .AsNoTracking()
            .Where(reading => reading.ContractId == contractId)
            .OrderByDescending(reading => reading.ReadingMonth)
            .Select(reading => new LatestReadingDto(reading.ReadingMonth, reading.ElectricEnd, reading.WaterEnd))
            .FirstOrDefaultAsync();

    public async Task<bool> AddIfLatestAsync(UtilityReadings reading)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (_context.Database.IsNpgsql())
        {
            // Khoá hợp đồng: hai người nhập chỉ số cùng lúc cho một hợp đồng sẽ phải xếp hàng,
            // người sau kiểm tra lại sẽ thấy tháng vừa ghi và bị từ chối.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {reading.ContractId} FOR UPDATE");
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

    public Task<List<UtilityReadingDto>> GetAllAsync(int? customerId, int? ownerId, int? contractId) =>
        InScope(customerId, ownerId)
            .Where(reading => contractId == null || reading.ContractId == contractId)
            .OrderByDescending(reading => reading.ReadingMonth)
            .ThenByDescending(reading => reading.ReadingId)
            .Select(ToDto)
            .ToListAsync();

    public Task<UtilityReadingDto?> GetByIdAsync(int readingId, int? customerId, int? ownerId) =>
        InScope(customerId, ownerId)
            .Where(reading => reading.ReadingId == readingId)
            .Select(ToDto)
            .FirstOrDefaultAsync();

    public Task<bool> ExistsAsync(int readingId) =>
        _context.UtilityReadings.AnyAsync(reading => reading.ReadingId == readingId);

    // Lọc dùng chung cho GetById và GetAll; tham số nào null thì không lọc theo tiêu chí đó.
    private IQueryable<UtilityReadings> InScope(int? customerId, int? ownerId) =>
        _context.UtilityReadings
            .AsNoTracking()
            .Where(reading => customerId == null || reading.Contract!.CustomerId == customerId)
            .Where(reading => ownerId == null || reading.Contract!.Room!.Property!.OwnerId == ownerId);

    private static readonly Expression<Func<UtilityReadings, UtilityReadingDto>> ToDto = reading => new UtilityReadingDto(
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
