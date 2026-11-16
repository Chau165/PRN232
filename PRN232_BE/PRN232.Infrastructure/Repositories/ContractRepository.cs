using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Policies;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class ContractRepository : IContractRepository
{
    private static readonly SemaphoreSlim SqliteContractWriteGate = new(1, 1);
    private readonly AppDbContext _context;
    private readonly ICccdHasher? _cccdHasher;

    public ContractRepository(AppDbContext context, ICccdHasher? cccdHasher = null)
    {
        _context = context;
        _cccdHasher = cccdHasher;
    }

    public Task<ContractBookingReadModel?> GetBookingAsync(int bookingId)
    {
        return _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.BookingId == bookingId)
            .Select(booking => new ContractBookingReadModel(
                booking.BookingId,
                booking.RoomId,
                booking.Room != null ? booking.Room.RoomCode : "",
                booking.CustomerId,
                booking.Property!.OwnerId,
                booking.Status,
                booking.DepositAmount,
                booking.AgreedMonthlyRent,
                booking.Contract != null,
                booking.Payments.Any(payment =>
                    payment.Status == PaymentStatuses.Success &&
                    payment.UserId == booking.CustomerId &&
                    payment.Amount == booking.DepositAmount),
                booking.Property!.Owner!.FullName,
                booking.Property!.AddressText,
                booking.PropertyId,
                booking.Customer!.FullName,
                booking.OccupantCount,
                booking.Room != null ? booking.Room.MaxOccupants : booking.PropertyRoomType!.Rooms.Select(room => (int?)room.MaxOccupants).Max() ?? 1,
                booking.RoomTypeId,
                booking.PropertyRoomType!.RoomType!.TypeName))
            .FirstOrDefaultAsync();
    }

    public async Task<ContractMutationResult> SaveMeterHandoverAsync(
        int contractId, int managerId, DateOnly handoverDate,
        decimal initialElectricReading, decimal initialWaterReading,
        DateOnly? expectedHandoverDate, decimal? expectedElectricReading, decimal? expectedWaterReading,
        DateOnly today, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {contractId} FOR UPDATE", cancellationToken);
        }
        var existingContract = await _context.Contracts.SingleOrDefaultAsync(
            contract => contract.ContractId == contractId, cancellationToken);
        if (existingContract is null)
        {
            return new ContractMutationResult(ContractMutationStatus.NotFound);
        }
        // A context can already track an older contract; refresh only after acquiring its row lock.
        await _context.Entry(existingContract).ReloadAsync(cancellationToken);
        var propertyId = await GetPropertyIdForRoomAsync(existingContract.RoomId, cancellationToken);
        if (!propertyId.HasValue)
        {
            return new ContractMutationResult(ContractMutationStatus.Conflict);
        }
        await LockManagerAssignmentAsync(propertyId.Value, managerId, cancellationToken);
        if (!await HasActiveManagerAssignmentAsync(propertyId.Value, managerId, cancellationToken))
        {
            return new ContractMutationResult(ContractMutationStatus.Forbidden);
        }
        if (existingContract.Status != ContractStatuses.Active ||
            !UtilityReadingPolicy.IsValidMeterValue(initialElectricReading) || !UtilityReadingPolicy.IsValidMeterValue(initialWaterReading) ||
            handoverDate < existingContract.StartDate || handoverDate > existingContract.EndDate || handoverDate > today)
        {
            return new ContractMutationResult(ContractMutationStatus.Conflict);
        }
        var unchanged = existingContract.MeterHandoverDate == handoverDate &&
            existingContract.InitialElectricReading == initialElectricReading &&
            existingContract.InitialWaterReading == initialWaterReading;
        if (!unchanged)
        {
            if (existingContract.MeterHandoverDate != expectedHandoverDate ||
                existingContract.InitialElectricReading != expectedElectricReading ||
                existingContract.InitialWaterReading != expectedWaterReading ||
                await _context.UtilityReadings.AnyAsync(reading => reading.ContractId == contractId, cancellationToken) ||
                await _context.Invoices.AnyAsync(invoice => invoice.ContractId == contractId, cancellationToken))
            {
                return new ContractMutationResult(ContractMutationStatus.Conflict);
            }
            existingContract.MeterHandoverDate = handoverDate;
            existingContract.InitialElectricReading = initialElectricReading;
            existingContract.InitialWaterReading = initialWaterReading;
            await _context.SaveChangesAsync(cancellationToken);
        }
        var updatedContract = await GetMutationReadModelAsync(contractId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ContractMutationResult(ContractMutationStatus.Updated, updatedContract);
    }

    public async Task<ContractMutationResult> SaveRegisteredVehicleCountAsync(
        int contractId, int managerId, int registeredVehicleCount, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {contractId} FOR UPDATE", cancellationToken);
        }
        var contract = await _context.Contracts.SingleOrDefaultAsync(
            item => item.ContractId == contractId, cancellationToken);
        if (contract is null)
        {
            return new ContractMutationResult(ContractMutationStatus.NotFound);
        }
        await _context.Entry(contract).ReloadAsync(cancellationToken);
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
        if (contract.Status != ContractStatuses.Active || registeredVehicleCount < 0)
        {
            return new ContractMutationResult(ContractMutationStatus.Conflict);
        }
        var activeOccupantCount = 1 + await _context.ContractOccupants.CountAsync(
            occupant => occupant.ContractId == contractId && occupant.MoveOutDate == null, cancellationToken);
        if (registeredVehicleCount > activeOccupantCount)
        {
            return new ContractMutationResult(ContractMutationStatus.Conflict);
        }
        contract.RegisteredVehicleCount = registeredVehicleCount;
        await _context.SaveChangesAsync(cancellationToken);
        var updated = await GetMutationReadModelAsync(contractId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ContractMutationResult(ContractMutationStatus.Updated, updated);
    }

    public Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId)
    {
        return _context.PropertyManagerAssignments.AnyAsync(assignment =>
            assignment.PropertyId == propertyId &&
            assignment.ManagerId == managerId &&
            assignment.Manager!.IsActive &&
            assignment.Manager.Role!.RoleName == RoleNames.Manager);
    }

    public async Task<ContractCreationStatus> AddFromBookingAsync(
        ContractCreationCommand creation, Notifications customerNotification)
    {
        var contract = creation.Contract;
        if (!contract.BookingId.HasValue)
        {
            return ContractCreationStatus.Conflict;
        }
        if (_cccdHasher is null)
        {
            throw new InvalidOperationException("An ICccdHasher is required to create contracts with a holder identity.");
        }
        var holderDocument = creation.PendingHolderIdentityDocument;
        holderDocument.CCCDNumberHash = _cccdHasher.Hash(holderDocument.CCCDNumber);
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await BookingPaymentLock.LockAsync(_context, contract.BookingId.Value);
        var location = await _context.Bookings.AsNoTracking().Where(booking => booking.BookingId == contract.BookingId)
            .Select(booking => new { booking.PropertyId, booking.RoomTypeId, booking.RoomId }).SingleOrDefaultAsync();
        if (location is null || location.RoomId.HasValue && location.RoomId != contract.RoomId ||
            !await _context.Rooms.AnyAsync(room => room.RoomId == contract.RoomId &&
                room.PropertyId == location.PropertyId && room.RoomTypeId == location.RoomTypeId))
            return ContractCreationStatus.Conflict;

        if (_context.Database.IsNpgsql())
        {
            // Inventory and booking are already locked; protect the current Manager assignment.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT assignment.\"PropertyId\" FROM \"PropertyManagerAssignments\" assignment JOIN \"Rooms\" room ON room.\"PropertyId\" = assignment.\"PropertyId\" WHERE room.\"RoomId\" = {contract.RoomId} AND assignment.\"ManagerId\" = {contract.ProcessedByStaffId} FOR SHARE OF assignment");
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({holderDocument.CCCDNumberHash}))");
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
            (booking.RoomId == null || booking.RoomId == contract.RoomId) &&
            booking.PropertyId == _context.Rooms.Where(room => room.RoomId == contract.RoomId).Select(room => room.PropertyId).FirstOrDefault() &&
            booking.RoomTypeId == _context.Rooms.Where(room => room.RoomId == contract.RoomId).Select(room => room.RoomTypeId).FirstOrDefault() &&
            booking.AgreedMonthlyRent == contract.MonthlyRent && booking.DepositAmount == contract.DepositAmount &&
            booking.CustomerId == contract.CustomerId &&
            booking.Status == BookingStatuses.Deposited &&
            !booking.RefundRequests.Any(refund => refund.Status != BookingRefundRequestStatuses.Rejected) &&
            !booking.PaymentRequests.Any(request => request.Status == PaymentRequestStatuses.Creating ||
                request.Status == PaymentRequestStatuses.Pending || request.Status == PaymentRequestStatuses.RequiresReview) &&
            booking.Payments.Count(payment => payment.Status == PaymentStatuses.Success) == 1 &&
            booking.Payments.Any(payment =>
                payment.Status == PaymentStatuses.Success &&
                payment.UserId == booking.CustomerId &&
                payment.Amount == booking.DepositAmount) &&
            booking.Contract == null);
        var bookingFacts = await _context.Bookings
            .Where(booking => booking.BookingId == contract.BookingId)
            .Select(booking => new { booking.RoomId, booking.PropertyId, booking.RoomTypeId, booking.OccupantCount,
                MaxOccupants = _context.Rooms.Where(room => room.RoomId == contract.RoomId).Select(room => room.MaxOccupants).FirstOrDefault() })
            .SingleOrDefaultAsync();
        if (bookingFacts is null || bookingFacts.RoomId == null &&
            (!creation.CustomerRoomSelectionAcknowledged || creation.RoomSelectionConfirmedAtUtc == null))
            return ContractCreationStatus.Conflict;
        var registeredOccupantCount = creation.Occupants.Count + 1;
        if (bookingFacts is null || registeredOccupantCount > bookingFacts.MaxOccupants ||
            (registeredOccupantCount != (bookingFacts.OccupantCount ?? 1) && !creation.OccupantCountChangeConfirmed) ||
            contract.RegisteredVehicleCount is not int vehicleCount || vehicleCount < 0 || vehicleCount > registeredOccupantCount)
        {
            return ContractCreationStatus.Conflict;
        }
        var roomHasUnterminatedContract = await _context.Contracts.AnyAsync(existing =>
            existing.RoomId == contract.RoomId && existing.Status != ContractStatuses.Terminated);
        if (!bookingStillDeposited || !managerAssignmentStillActive || roomHasUnterminatedContract)
        {
            return ContractCreationStatus.Conflict;
        }

<<<<<<< HEAD
        var allocationInstant = new DateTimeOffset(DateTime.SpecifyKind(
            creation.RoomSelectionConfirmedAtUtc ?? DateTime.UtcNow, DateTimeKind.Utc));
        if (!await RoomInventoryQueries.CanReserveAsync(_context, bookingFacts.PropertyId, bookingFacts.RoomTypeId,
                Math.Max(registeredOccupantCount, bookingFacts.OccupantCount ?? 1), contract.RoomId,
                contract.BookingId, allocationInstant)) return ContractCreationStatus.Conflict;
=======
        // Recheck the reviewed source under the same Booking -> Room -> Assignment -> Application locks.
        var application=await _context.BookingApplications.Where(a=>a.BookingId==contract.BookingId).Select(a=>new {a.BookingApplicationId}).SingleOrDefaultAsync();
        if(application is not null) {
            if(_context.Database.IsNpgsql())await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"BookingApplications\" WHERE \"BookingApplicationId\"={application.BookingApplicationId} FOR UPDATE");
            var fresh=await _context.BookingApplications.AsNoTracking().Include(a=>a.People).ThenInclude(p=>p.Images).SingleAsync(a=>a.BookingApplicationId==application.BookingApplicationId);
            if(creation.BookingApplicationVersion!=fresh.Version || fresh.CustomerId!=contract.CustomerId || fresh.RoomId!=contract.RoomId ||
                !PRN232.Application.Policies.BookingApplicationPolicy.ReadyForContract(fresh) || fresh.OccupantCount!=registeredOccupantCount ||
                fresh.People.Single(p=>p.Position==0).IdentityNumber!=holderDocument.CCCDNumber ||
                !fresh.People.Where(p=>p.Position>0).OrderBy(p=>p.Position).Select(p=>(p.FullName.Trim(),p.IdentityNumber)).SequenceEqual(creation.Occupants.Select(p=>(p.FullName,p.IdentityNumber))))
                return ContractCreationStatus.Conflict;
            var stamp=DateTime.UtcNow;
            foreach(var person in fresh.People)contract.IdentitySnapshots.Add(new() {
                SourceApplicationId=fresh.BookingApplicationId,SourceApplicationVersion=fresh.Version,SourcePersonId=person.BookingApplicationPersonId,SourceRevision=person.Revision,
                Position=person.Position,FullName=person.FullName,IdentityNumber=person.IdentityNumber,
                FrontObjectPath=person.Images.Single(i=>i.IsCurrent&&i.Side=="Front"&&i.PersonRevision==person.Revision).ObjectPath,
                BackObjectPath=person.Images.Single(i=>i.IsCurrent&&i.Side=="Back"&&i.PersonRevision==person.Revision).ObjectPath,CreatedAtUtc=stamp
            });
            var convertedVersion=Guid.NewGuid();
            if(await _context.BookingApplications.Where(a=>a.BookingApplicationId==fresh.BookingApplicationId&&a.Version==fresh.Version)
                .ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,"Converted").SetProperty(a=>a.Version,convertedVersion).SetProperty(a=>a.UpdatedAtUtc,stamp))!=1)return ContractCreationStatus.Conflict;
        } else if(creation.BookingApplicationVersion.HasValue)return ContractCreationStatus.Conflict;
>>>>>>> origin/main

        var existingHolderDocuments = await _context.IdentityDocuments
            .Where(document => document.CustomerId == contract.CustomerId)
            .Select(document => new { document.CCCDNumberHash, document.Status })
            .ToListAsync();
        var activeHolderDocuments = existingHolderDocuments.Where(document =>
            document.Status == IdentityDocumentStatuses.Pending || document.Status == IdentityDocumentStatuses.Approved).ToList();
        if (activeHolderDocuments.Any(document => document.CCCDNumberHash != holderDocument.CCCDNumberHash) ||
            await _context.IdentityDocuments.AnyAsync(document => document.CustomerId != contract.CustomerId &&
                document.CCCDNumberHash == holderDocument.CCCDNumberHash))
        {
            return ContractCreationStatus.HolderIdentityConflict;
        }

        contract.Occupants = creation.Occupants.ToList();
        foreach (var occupant in creation.Occupants)
        {
            occupant.Contract = contract;
        }
        // Assignment, consent evidence, contract, room state and notifications share this transaction.
        // ExecuteUpdate avoids leaking a tracked RoomId assignment if a later insert is rolled back.
        var assigned = await _context.Bookings.Where(booking => booking.BookingId == contract.BookingId &&
                (booking.RoomId == null || booking.RoomId == contract.RoomId) && booking.Contract == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(booking => booking.RoomId, contract.RoomId));
        if (assigned != 1) return ContractCreationStatus.Conflict;
        if (creation.CustomerRoomSelectionAcknowledged)
            _context.Set<AuditLogs>().Add(new AuditLogs
            {
                UserId = contract.ProcessedByStaffId, Role = RoleNames.Manager, Action = "ConfirmContractRoomSelection",
                HttpMethod = "POST", Path = "/api/contracts", EntityType = "Booking", EntityId = contract.BookingId.ToString(),
                StatusCode = 201, Succeeded = true, CreatedAtUtc = creation.RoomSelectionConfirmedAtUtc!.Value,
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { BookingId = contract.BookingId,
                    RoomId = contract.RoomId, ManagerId = contract.ProcessedByStaffId,
                    ConfirmedAtUtc = creation.RoomSelectionConfirmedAtUtc, CustomerAgreed = true })
            });
        var roomCode = await _context.Rooms.Where(room => room.RoomId == contract.RoomId).Select(room => room.RoomCode).SingleAsync();
        customerNotification.Title = $"Hợp đồng thuê phòng {roomCode} đã được lập";
        await _context.Contracts.AddAsync(contract);
        if (activeHolderDocuments.Count == 0)
        {
            await _context.IdentityDocuments.AddAsync(holderDocument);
        }
        await _context.Notifications.AddAsync(customerNotification);
        await _context.SaveChangesAsync();
        // Phòng đã có người thuê: màn hình quản lý phòng và "Phòng của tôi" đọc Room.Status sẽ hiển thị đúng.
        await _context.Rooms
            .Where(room => room.RoomId == contract.RoomId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(room => room.Status, RoomStatuses.Occupied));
        await transaction.CommitAsync();
        return ContractCreationStatus.Created;
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
                DepositAmount = invoice.Booking == null ? invoice.DepositAmount : invoice.Booking.DepositAmount,
                DepositAppliesToFirstMonth = invoice.Booking != null && invoice.Booking.DepositAppliesToFirstMonth,
                invoice.TransferredFromContractId,
                invoice.ManualSecurityDepositAmount
            })
            .ToListAsync(cancellationToken);
        // Rebuild 4.2: hợp đồng đã chuyển người đứng tên thì cọc nằm ở hợp đồng kế tiếp.
        var successors = await _context.Contracts
            .AsNoTracking()
            .Where(item => item.TransferredFromContractId != null && contractIds.Contains(item.TransferredFromContractId.Value))
            .Select(item => new { From = item.TransferredFromContractId!.Value, item.ContractId })
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
            // Rebuild 3.3: cùng số tiền cọc đã đóng, luật cũ coi là tiền phòng trả trước, luật mới coi là
            // tiền đảm bảo giữ đến khi trả phòng.
            var prepaid = booking?.DepositAppliesToFirstMonth == true;
            // Rebuild 4.2: hợp đồng nhận chuyển giữ đúng số cọc được chuyển (DepositAmount); hợp đồng đã chuyển
            // đi không còn giữ cọc.
            var successor = successors.FirstOrDefault(item => item.From == contract.ContractId)?.ContractId;
            var securityDeposit = successor is not null ? 0m
                : booking?.TransferredFromContractId is not null ? contract.DepositAmount
                : prepaid ? 0m : eligiblePaid;
            if (successor is null)
            {
                securityDeposit += booking?.ManualSecurityDepositAmount ?? 0m;
            }
            return contract with
            {
                PrepaidRentAmount = prepaid ? eligiblePaid : 0m,
                PrepaidRentApplied = applied,
                RemainingPrepaidCredit = prepaid ? Math.Max(0m, eligiblePaid - applied) : 0m,
                SecurityDepositAmount = securityDeposit,
                TransferredToContractId = successor
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
        contract.Room.Property.AddressText,
        contract.InitialElectricReading,
        contract.InitialWaterReading,
        contract.MeterHandoverDate,
        contract.UtilityReadings.Any(),
        contract.Invoices.Any(),
        0m,
        contract.PlannedMoveOutDate,
        contract.MoveOutNoticeAtUtc,
        contract.TransferredFromContractId,
        null,
        contract.RegisteredVehicleCount,
        contract.ManualSecurityDepositAmount,
        contract.SecurityDepositAcknowledgedAtUtc,
        contract.SecurityDepositRecordedByManagerId);

    public async Task<bool> SaveMoveOutNoticeAsync(int contractId, DateOnly? plannedMoveOutDate, DateTime? noticeAtUtc,
        int? noticeById, IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var updated = await _context.Contracts
            .Where(contract => contract.ContractId == contractId && contract.Status == ContractStatuses.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(contract => contract.PlannedMoveOutDate, plannedMoveOutDate)
                .SetProperty(contract => contract.MoveOutNoticeAtUtc, noticeAtUtc)
                .SetProperty(contract => contract.MoveOutNoticeById, noticeById), cancellationToken);
        if (updated == 0)
        {
            return false;
        }
        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task<List<int>> GetActiveManagerIdsAsync(int propertyId, CancellationToken cancellationToken) =>
        _context.PropertyManagerAssignments
            .AsNoTracking()
            .Where(assignment => assignment.PropertyId == propertyId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == RoleNames.Manager)
            .Select(assignment => assignment.ManagerId)
            .ToListAsync(cancellationToken);
}
