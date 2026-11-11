using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class ContractRepository : IContractRepository
{
    private readonly AppDbContext _context;

    public ContractRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ContractBookingReadModel?> GetBookingAsync(int bookingId)
    {
        return _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.BookingId == bookingId)
            .Select(booking => new ContractBookingReadModel(
            booking.BookingId,
            booking.RoomId,
            booking.Room!.RoomCode,
            booking.CustomerId,
            booking.Room.Property!.OwnerId,
            booking.Status,
            booking.DepositAmount,
            booking.Room.RoomType!.Price,
            booking.Contract != null))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> AddFromBookingAsync(Contracts contract, Notifications customerNotification)
    {
        if (!contract.BookingId.HasValue) return false;
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await BookingPaymentLock.LockAsync(_context, contract.BookingId.Value);

        var bookingStillDeposited = await _context.Bookings.AnyAsync(booking =>
            booking.BookingId == contract.BookingId &&
            booking.RoomId == contract.RoomId &&
            booking.Status == BookingStatuses.Deposited &&
            booking.Contract == null);
        var roomHasActiveContract = await _context.Contracts.AnyAsync(existing =>
            existing.RoomId == contract.RoomId && existing.Status == ContractStatuses.Active);
        if (!bookingStillDeposited || roomHasActiveContract)
        {
            return false;
        }

        await _context.Contracts.AddAsync(contract);
        await _context.Notifications.AddAsync(customerNotification);
        await _context.SaveChangesAsync();
        // Phòng đã có người thuê: màn hình quản lý phòng và "Phòng của tôi" đọc Room.Status sẽ hiển thị đúng.
        await _context.Rooms
            .Where(room => room.RoomId == contract.RoomId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(room => room.Status, RoomStatuses.Occupied));
        await transaction.CommitAsync();
        return true;
    }

    public Task<List<ContractReadModel>> GetAllAsync(int? customerId, int? ownerId)
    {
        return InScope(customerId, ownerId)
            .OrderByDescending(contract => contract.ContractId)
            .Select(ToReadModel)
            .ToListAsync();
    }

    public Task<ContractReadModel?> GetByIdAsync(int contractId, int? customerId, int? ownerId)
    {
        return InScope(customerId, ownerId)
            .Where(contract => contract.ContractId == contractId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync();
    }

    public Task<bool> ExistsAsync(int contractId)
    {
        return _context.Contracts.AnyAsync(contract => contract.ContractId == contractId);
    }

    // Lọc dùng chung cho GetById và GetAll; tham số nào null thì không lọc theo tiêu chí đó.
    private IQueryable<Contracts> InScope(int? customerId, int? ownerId)
    {
        return _context.Contracts
            .AsNoTracking()
            .Where(contract => customerId == null || contract.CustomerId == customerId)
            .Where(contract => ownerId == null || contract.Room!.Property!.OwnerId == ownerId);
    }

    private static readonly Expression<Func<Contracts, ContractReadModel>> ToReadModel = contract => new ContractReadModel(
        contract.ContractId,
        contract.BookingId,
        contract.RoomId,
        contract.Room!.RoomCode,
        contract.Room.PropertyId,
        contract.Room.Property!.PropertyName,
        contract.CustomerId,
        contract.Customer!.FullName,
        contract.Customer.PhoneNumber,
        contract.StartDate,
        contract.EndDate,
        contract.MonthlyRent,
        contract.DepositAmount,
        contract.Status,
        contract.ProcessedByStaff != null ? contract.ProcessedByStaff.FullName : null);
}
