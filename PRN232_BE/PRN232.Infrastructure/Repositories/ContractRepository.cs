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
    private static readonly SemaphoreSlim SqliteContractWriteGate = new(1, 1);
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
                booking.Contract != null,
                booking.Payments.Any(payment =>
                    payment.Status == PaymentStatuses.Success &&
                    payment.UserId == booking.CustomerId &&
                    payment.Amount == booking.DepositAmount),
                booking.Room.Property.Owner!.FullName,
                booking.Room.Property.AddressText,
                booking.Room.PropertyId))
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

    public async Task<bool> AddFromBookingAsync(Contracts contract, Notifications customerNotification)
    {
        if (!contract.BookingId.HasValue)
        {
            return false;
        }
        await using var transaction = await _context.Database.BeginTransactionAsync();
        //Nghia
        await BookingPaymentLock.LockAsync(_context, contract.BookingId.Value);
        //Nghia


        if (_context.Database.IsNpgsql())
        {
            // Khoá booking để không ai huỷ nó giữa chừng (huỷ booking cũng khoá dòng này),
            // và khoá phòng để hai hợp đồng không thể cùng được lập cho một phòng.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Bookings\" WHERE \"BookingId\" = {contract.BookingId} FOR UPDATE");
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Rooms\" WHERE \"RoomId\" = {contract.RoomId} FOR UPDATE");
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT assignment.\"PropertyId\" FROM \"PropertyManagerAssignments\" assignment JOIN \"Rooms\" room ON room.\"PropertyId\" = assignment.\"PropertyId\" WHERE room.\"RoomId\" = {contract.RoomId} AND assignment.\"ManagerId\" = {contract.ProcessedByStaffId} FOR SHARE OF assignment");
        }

        var managerAssignmentStillActive = contract.ProcessedByStaffId.HasValue &&
            await _context.PropertyManagerAssignments.AnyAsync(assignment =>
                assignment.PropertyId == _context.Rooms
                    .Where(room => room.RoomId == contract.RoomId)
                    .Select(room => room.PropertyId)
                    .FirstOrDefault() &&
                assignment.ManagerId == contract.ProcessedByStaffId.Value &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == RoleNames.Manager);

        var bookingStillDeposited = await _context.Bookings.AnyAsync(booking =>
            booking.BookingId == contract.BookingId &&
            booking.RoomId == contract.RoomId &&
            booking.CustomerId == contract.CustomerId &&
            booking.Status == BookingStatuses.Deposited &&
            booking.Payments.Any(payment =>
                payment.Status == PaymentStatuses.Success &&
                payment.UserId == booking.CustomerId &&
                payment.Amount == booking.DepositAmount) &&
            booking.Contract == null);
        var roomHasUnterminatedContract = await _context.Contracts.AnyAsync(existing =>
            existing.RoomId == contract.RoomId && existing.Status != ContractStatuses.Terminated);
        if (!bookingStillDeposited || !managerAssignmentStillActive || roomHasUnterminatedContract)
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

    public async Task<List<ContractReadModel>> GetAllAsync(int? customerId, int? managerId)
    {
        var contracts = await InScope(customerId, managerId)
            .OrderByDescending(contract => contract.ContractId)
            .Select(ToReadModel)
            .ToListAsync();
        return await IncludeDerivedCreditAsync(contracts);
    }

    public async Task<ContractReadModel?> GetByIdAsync(int contractId, int? customerId, int? managerId)
    {
        var contract = await InScope(customerId, managerId)
            .Where(contract => contract.ContractId == contractId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync();
        if (contract is null)
        {
            return null;
        }

        return (await IncludeDerivedCreditAsync([contract]))[0];
    }

    public async Task<List<InvoiceReadModel>> GetIssuedInvoicesForTimelineAsync(
        int contractId,
        CancellationToken cancellationToken)
    {
        return await GetContractInvoicesAsync(contractId, cancellationToken);
    }

    public async Task<ContractMutationResult> ExtendAsync(
        int contractId,
        int managerId,
        DateOnly newEndDate,
        DateOnly today,
        DateTimeOffset asOfUtc,
        Notifications customerNotification,
        CancellationToken cancellationToken)
    {
        var usesSqliteGate = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (usesSqliteGate)
        {
            await SqliteContractWriteGate.WaitAsync(cancellationToken);
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var contract = await LockContractAndRoomAsync(contractId, cancellationToken);
            if (contract is null)
            {
                return new ContractMutationResult(ContractMutationStatus.NotFound);
            }

            var propertyId = await GetPropertyIdForRoomAsync(contract.RoomId, cancellationToken);
            if (!propertyId.HasValue)
            {
                return new ContractMutationResult(ContractMutationStatus.Conflict);
            }

            await LockManagerAssignmentAsync(propertyId.Value, managerId, cancellationToken);
            if (!await HasActiveManagerAssignmentAsync(propertyId.Value, managerId, cancellationToken))
            {
                return new ContractMutationResult(ContractMutationStatus.Forbidden);
            }

            if (contract.Status != ContractStatuses.Active ||
                newEndDate <= contract.EndDate ||
                newEndDate <= today)
            {
                return new ContractMutationResult(ContractMutationStatus.Conflict);
            }

            var room = await _context.Rooms
                .SingleOrDefaultAsync(item => item.RoomId == contract.RoomId, cancellationToken);
            if (room is null ||
                room.Status is not (RoomStatuses.Occupied or RoomStatuses.Maintenance) ||
                await HasOtherRoomHolderAsync(contract, asOfUtc, cancellationToken))
            {
                return new ContractMutationResult(ContractMutationStatus.Conflict);
            }

            contract.EndDate = newEndDate;
            await _context.Notifications.AddAsync(customerNotification, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var updatedContract = await GetMutationReadModelAsync(contractId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ContractMutationResult(ContractMutationStatus.Updated, updatedContract);
        }
        finally
        {
            if (usesSqliteGate)
            {
                SqliteContractWriteGate.Release();
            }
        }
    }

    public async Task<ContractMutationResult> TerminateAsync(
        int contractId,
        int managerId,
        bool roomReadyForRental,
        bool outstandingDebtAcknowledged,
        DateTimeOffset asOfUtc,
        Notifications customerNotification,
        CancellationToken cancellationToken)
    {
        var usesSqliteGate = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (usesSqliteGate)
        {
            await SqliteContractWriteGate.WaitAsync(cancellationToken);
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var contract = await LockContractAndRoomAsync(contractId, cancellationToken);
            if (contract is null)
            {
                return new ContractMutationResult(ContractMutationStatus.NotFound);
            }

            var propertyId = await GetPropertyIdForRoomAsync(contract.RoomId, cancellationToken);
            if (!propertyId.HasValue)
            {
                return new ContractMutationResult(ContractMutationStatus.Conflict);
            }

            if (_context.Database.IsNpgsql())
            {
                // Payment processing locks Invoice before PropertyManagerAssignments; keep the same order.
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT \"InvoiceId\" FROM \"Invoices\" WHERE \"ContractId\" = {contractId} FOR UPDATE",
                    cancellationToken);
            }

            var invoices = await GetContractInvoicesAsync(contractId, cancellationToken);
            var outstandingDebt = invoices.Sum(invoice => invoice.RemainingPayable);
            await LockManagerAssignmentAsync(propertyId.Value, managerId, cancellationToken);
            if (!await HasActiveManagerAssignmentAsync(propertyId.Value, managerId, cancellationToken))
            {
                return new ContractMutationResult(ContractMutationStatus.Forbidden);
            }

            if (contract.Status != ContractStatuses.Active)
            {
                return new ContractMutationResult(ContractMutationStatus.Conflict);
            }

            var room = await _context.Rooms
                .SingleOrDefaultAsync(item => item.RoomId == contract.RoomId, cancellationToken);
            if (room is null ||
                room.Status == RoomStatuses.Reserved ||
                !RoomStatuses.All.Contains(room.Status) ||
                await HasOtherRoomHolderAsync(contract, asOfUtc, cancellationToken))
            {
                return new ContractMutationResult(ContractMutationStatus.Conflict);
            }

            if (outstandingDebt > 0m && !outstandingDebtAcknowledged)
            {
                return new ContractMutationResult(
                    ContractMutationStatus.Conflict,
                    OutstandingDebt: outstandingDebt);
            }

            contract.Status = ContractStatuses.Terminated;
            room.Status = roomReadyForRental ? RoomStatuses.Available : RoomStatuses.Maintenance;
            await _context.Notifications.AddAsync(customerNotification, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var terminatedContract = await GetMutationReadModelAsync(contractId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ContractMutationResult(
                ContractMutationStatus.Updated,
                terminatedContract,
                outstandingDebt);
        }
        finally
        {
            if (usesSqliteGate)
            {
                SqliteContractWriteGate.Release();
            }
        }
    }

    public async Task<ContractMutationStatus> MarkTerminatedRoomReadyAsync(
        int contractId,
        int managerId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        var usesSqliteGate = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        if (usesSqliteGate)
        {
            await SqliteContractWriteGate.WaitAsync(cancellationToken);
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var contract = await LockContractAndRoomAsync(contractId, cancellationToken);
            if (contract is null)
            {
                return ContractMutationStatus.NotFound;
            }

            var propertyId = await GetPropertyIdForRoomAsync(contract.RoomId, cancellationToken);
            if (!propertyId.HasValue)
            {
                return ContractMutationStatus.Conflict;
            }

            await LockManagerAssignmentAsync(propertyId.Value, managerId, cancellationToken);
            if (!await HasActiveManagerAssignmentAsync(propertyId.Value, managerId, cancellationToken))
            {
                return ContractMutationStatus.Forbidden;
            }
            if (contract.Status != ContractStatuses.Terminated)
            {
                return ContractMutationStatus.Conflict;
            }

            var room = await _context.Rooms
                .SingleOrDefaultAsync(item => item.RoomId == contract.RoomId, cancellationToken);
            if (room is null)
            {
                return ContractMutationStatus.Conflict;
            }
            if (room.Status == RoomStatuses.Available)
            {
                await transaction.CommitAsync(cancellationToken);
                return ContractMutationStatus.Updated;
            }
            if (room.Status is not (RoomStatuses.Maintenance or RoomStatuses.Occupied) ||
                await HasOtherRoomHolderAsync(contract, asOfUtc, cancellationToken))
            {
                return ContractMutationStatus.Conflict;
            }

            room.Status = RoomStatuses.Available;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ContractMutationStatus.Updated;
        }
        finally
        {
            if (usesSqliteGate)
            {
                SqliteContractWriteGate.Release();
            }
        }
    }

    private async Task<List<InvoiceReadModel>> GetContractInvoicesAsync(
        int contractId,
        CancellationToken cancellationToken)
    {
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.ContractId == contractId)
            .OrderBy(invoice => invoice.InvoiceMonth)
            .Select(invoice => new InvoiceReadModel(
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
                        detail.ItemType,
                        detail.Description,
                        detail.Quantity,
                        detail.UnitPrice,
                        detail.UnitCode,
                        detail.Amount))
                    .ToList(),
                0m,
                0m,
                null,
                null))
            .ToListAsync(cancellationToken);

        return await InvoiceReadModelEnricher.WithTotalsAndPaymentsAsync(
            _context,
            invoices,
            cancellationToken);
    }

    private async Task<Contracts?> LockContractAndRoomAsync(
        int contractId,
        CancellationToken cancellationToken)
    {
        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {contractId} FOR UPDATE",
                cancellationToken);
        }

        var contract = await _context.Contracts
            .SingleOrDefaultAsync(item => item.ContractId == contractId, cancellationToken);
        if (contract is not null && _context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Rooms\" WHERE \"RoomId\" = {contract.RoomId} FOR UPDATE",
                cancellationToken);
        }

        return contract;
    }

    private async Task<int?> GetPropertyIdForRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        return await _context.Rooms
            .Where(room => room.RoomId == roomId)
            .Select(room => (int?)room.PropertyId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task LockManagerAssignmentAsync(
        int propertyId,
        int managerId,
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsNpgsql())
        {
            return;
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"UserId\" FROM \"Users\" WHERE \"UserId\" = {managerId} FOR SHARE",
            cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"PropertyId\" FROM \"PropertyManagerAssignments\" WHERE \"PropertyId\" = {propertyId} AND \"ManagerId\" = {managerId} FOR SHARE",
            cancellationToken);
    }

    private Task<bool> HasActiveManagerAssignmentAsync(
        int propertyId,
        int managerId,
        CancellationToken cancellationToken)
    {
        return _context.PropertyManagerAssignments.AnyAsync(assignment =>
            assignment.PropertyId == propertyId &&
            assignment.ManagerId == managerId &&
            assignment.Manager!.IsActive &&
            assignment.Manager.Role!.RoleName == RoleNames.Manager,
            cancellationToken);
    }

    private async Task<bool> HasOtherRoomHolderAsync(
        Contracts contract,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        var hasOtherContract = await _context.Contracts.AnyAsync(existing =>
            existing.RoomId == contract.RoomId &&
            existing.ContractId != contract.ContractId &&
            existing.Status != ContractStatuses.Terminated,
            cancellationToken);
        if (hasOtherContract)
        {
            return true;
        }

        var nowUtc = asOfUtc.UtcDateTime;
        return await _context.Bookings.AnyAsync(booking =>
            booking.RoomId == contract.RoomId &&
            booking.BookingId != contract.BookingId &&
            (booking.Status == BookingStatuses.Deposited ||
             (booking.Status == BookingStatuses.Pending && booking.ExpiryDate > nowUtc) ||
             (booking.Status != BookingStatuses.Pending &&
              booking.Status != BookingStatuses.Deposited &&
              booking.Status != BookingStatuses.Cancelled)),
            cancellationToken);
    }

    private async Task<ContractReadModel?> GetMutationReadModelAsync(
        int contractId,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Contracts
            .AsNoTracking()
            .Where(item => item.ContractId == contractId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync(cancellationToken);
        if (contract is null)
        {
            return null;
        }

        return (await IncludeDerivedCreditAsync([contract], cancellationToken))[0];
    }

    public Task<bool> ExistsAsync(int contractId)
    {
        return _context.Contracts.AnyAsync(contract => contract.ContractId == contractId);
    }

    private async Task<List<ContractReadModel>> IncludeDerivedCreditAsync(
        List<ContractReadModel> contracts,
        CancellationToken cancellationToken = default)
    {
        if (contracts.Count == 0)
        {
            return contracts;
        }

        var contractIds = contracts.Select(contract => contract.ContractId).ToArray();
        var bookingRows = await _context.Contracts
            .AsNoTracking()
            .Where(invoice => contractIds.Contains(invoice.ContractId))
            .Select(invoice => new
            {
                invoice.ContractId,
                invoice.BookingId,
                invoice.CustomerId,
                DepositAmount = invoice.Booking == null ? invoice.DepositAmount : invoice.Booking.DepositAmount
            })
            .ToListAsync(cancellationToken);
        var bookingIds = bookingRows
            .Where(contract => contract.BookingId.HasValue)
            .Select(contract => contract.BookingId!.Value)
            .ToArray();
        var paidRows = await _context.Payments
            .AsNoTracking()
            .Where(payment => payment.BookingId.HasValue &&
                bookingIds.Contains(payment.BookingId.Value) &&
                payment.Status == PaymentStatuses.Success)
            .Select(payment => new { BookingId = payment.BookingId!.Value, payment.UserId, payment.Amount })
            .ToListAsync(cancellationToken);
        var appliedRows = await _context.InvoiceDetails
            .AsNoTracking()
            .Where(detail => contractIds.Contains(detail.Invoice!.ContractId) &&
                detail.ItemType == InvoiceItemTypes.PrepaidRentCredit &&
                detail.Amount < 0m)
            .Select(detail => new { detail.Invoice!.ContractId, detail.Amount })
            .ToListAsync(cancellationToken);
        var appliedByContract = appliedRows
            .GroupBy(detail => detail.ContractId)
            .ToDictionary(group => group.Key, group => group.Sum(detail => -detail.Amount));

        return contracts.Select(contract =>
        {
            var booking = bookingRows.FirstOrDefault(row => row.ContractId == contract.ContractId);
            var eligiblePaid = booking?.BookingId is int bookingId
                ? Math.Min(
                    booking.DepositAmount,
                    paidRows
                        .Where(payment => payment.BookingId == bookingId && payment.UserId == booking.CustomerId)
                        .Sum(payment => payment.Amount))
                : 0m;
            appliedByContract.TryGetValue(contract.ContractId, out var applied);
            return contract with
            {
                PrepaidRentAmount = eligiblePaid,
                PrepaidRentApplied = applied,
                RemainingPrepaidCredit = Math.Max(0m, eligiblePaid - applied)
            };
        }).ToList();
    }

    // Lọc dùng chung cho GetById và GetAll; tham số nào null thì không lọc theo tiêu chí đó.
    private IQueryable<Contracts> InScope(int? customerId, int? managerId)
    {
        return _context.Contracts
            .AsNoTracking()
            .Where(contract => customerId == null || contract.CustomerId == customerId)
            .Where(contract => managerId == null || contract.Room!.Property!.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == RoleNames.Manager));
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
        contract.ProcessedByStaff != null ? contract.ProcessedByStaff.FullName : null,
        0m,
        0m,
        0m,
        contract.Room!.Property!.AddressText,
        contract.Room.Property.OwnerId,
        contract.Room.Property.Owner!.FullName,
        contract.Room.Property.AddressText);
}
