using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace PRN232.Application.Services;

public class ContractService : IContractService
{
    private const int MaximumPlannedPeriods = 120;
    // Ngày hợp đồng là ngày theo lịch Việt Nam, giống cách RentalService xác định "đang thuê".
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private readonly IContractRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly INotificationRealtimePublisher? _realtimePublisher;
    private readonly ILogger<ContractService>? _logger;
    private readonly IUserContactLookup? _contactLookup;

    public ContractService(
        IContractRepository repository,
        TimeProvider timeProvider,
        INotificationRealtimePublisher? realtimePublisher = null,
        ILogger<ContractService>? logger = null,
        IUserContactLookup? contactLookup = null)
    {
        _contactLookup = contactLookup;
        _repository = repository;
        _timeProvider = timeProvider;
        _realtimePublisher = realtimePublisher;
        _logger = logger;
    }

    public async Task<ContractDto> SaveMeterHandoverAsync(
        int contractId, SaveContractMeterHandoverDto request, int userId, string role,
        CancellationToken cancellationToken)
    {
        var contract = await GetContractForManagerMutationAsync(contractId, userId, role);
        if (!request.MeterHandoverDate.HasValue || !request.InitialElectricReading.HasValue ||
            !request.InitialWaterReading.HasValue)
        {
            throw new ContractBusinessRuleException("Vui lòng nhập đủ ngày và chỉ số điện nước bàn giao.");
        }
        if (contract.Status != ContractStatuses.Active)
        {
            throw new ContractConflictException("Chỉ hợp đồng đang hiệu lực mới được ghi nhận bàn giao.");
        }
        var today = TodayInBusinessTimeZone();
        if (!UtilityReadingPolicy.IsValidMeterValue(request.InitialElectricReading.Value) ||
            !UtilityReadingPolicy.IsValidMeterValue(request.InitialWaterReading.Value) ||
            request.MeterHandoverDate < contract.StartDate || request.MeterHandoverDate > contract.EndDate ||
            request.MeterHandoverDate > today)
        {
            throw new ContractBusinessRuleException("Chỉ số phải không âm, tối đa 16 chữ số nguyên và 2 chữ số thập phân; ngày bàn giao phải trong thời hạn hợp đồng và không ở tương lai.");
        }

        var expected = request.ExpectedMeterHandover;
        var result = await _repository.SaveMeterHandoverAsync(contractId, userId,
            request.MeterHandoverDate.Value, request.InitialElectricReading.Value, request.InitialWaterReading.Value,
            expected?.MeterHandoverDate, expected?.InitialElectricReading, expected?.InitialWaterReading,
            today, cancellationToken);
        if (result.Status == ContractMutationStatus.Conflict)
        {
            throw new ContractConflictException("Dữ liệu bàn giao đã thay đổi hoặc đã chốt chỉ số/hóa đơn. Vui lòng tải lại hợp đồng.");
        }
        var updated = GetUpdatedContract(result);
        _logger?.LogInformation("Manager {ManagerId} saved meter handover for contract {ContractId}; previous {PreviousDate}/{PreviousElectric}/{PreviousWater}, current {Date}/{Electric}/{Water}",
            userId, contractId, contract.MeterHandoverDate, contract.InitialElectricReading, contract.InitialWaterReading,
            updated.MeterHandoverDate, updated.InitialElectricReading, updated.InitialWaterReading);
        return ConvertToDto(updated);
    }

    public async Task<ContractDto> CreateFromBookingAsync(CreateContractDto dto, int userId, string role)
    {
        if (role != RoleNames.Manager)
        {
            throw new ContractAuthorizationException("Only an assigned Manager can create contracts.");
        }

        var booking = await _repository.GetBookingAsync(dto.BookingId);
        if (booking is null)
        {
            throw new ContractNotFoundException("Booking does not exist.");
        }
        if (!await _repository.HasActiveManagerAssignmentAsync(booking.PropertyId, userId))
        {
            throw new ContractAuthorizationException("You are not authorized to create contracts for this property.");
        }
        if (booking.Status != BookingStatuses.Deposited)
        {
            throw new ContractConflictException("Only deposited bookings can become contracts.");
        }
        if (booking.HasContract)
        {
            throw new ContractConflictException("This booking already has a contract.");
        }
        if (!booking.HasSuccessfulPayment || booking.DepositAmount <= 0m)
        {
            throw new ContractConflictException(
                "A successful Booking payment is required before creating a contract.");
        }
        if (booking.MonthlyRent is not > 0m)
        {
            throw new ContractConflictException(
                "This historical booking has no verified agreed rent. Reconcile the accepted rent before creating a contract.");
        }
        // The contract records the tenant's contact details.
        await PhoneNumberRequiredException.EnsureAsync(
            _contactLookup, booking.CustomerId, PhoneNumberRequiredException.CustomerMessage);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        if (dto.StartDate < today)
        {
            throw new ContractBusinessRuleException("Start date cannot be in the past.");
        }
        if (dto.EndDate <= dto.StartDate)
        {
            throw new ContractBusinessRuleException("End date must be after the start date.");
        }

        // Contract rent comes from the immutable booking snapshot; the deposit stays the paid amount.
        var contract = ConvertToEntity(dto, booking, userId);
        var notification = new Notifications
        {
            UserId = booking.CustomerId,
            Title = $"Hợp đồng thuê phòng {booking.RoomCode} đã được lập",
            Type = NotificationTypes.Contract,
            IsRead = false
        };
        if (!await _repository.AddFromBookingAsync(contract, notification))
        {
            throw new ContractConflictException("The booking changed or the room already has an active contract.");
        }
        if (_realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync([notification]);
        }
        var created = await _repository.GetByIdAsync(contract.ContractId, null, null);
        if (created is null)
        {
            throw new InvalidOperationException("Created contract was not found.");
        }
        return ConvertToDto(created);
    }

    public async Task<List<ContractDto>> GetAllAsync(int userId, string role)
    {
        var (customerId, managerId) = GetScope(userId, role);
        var contracts = await _repository.GetAllAsync(customerId, managerId);
        return contracts.Select(ConvertToDto).ToList();
    }

    public async Task<ContractDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, managerId) = GetScope(userId, role);
        var contract = await _repository.GetByIdAsync(id, customerId, managerId);
        if (contract is null && (customerId.HasValue || managerId.HasValue) && await _repository.ExistsAsync(id))
        {
            throw new ContractAuthorizationException("You cannot access this contract.");
        }
        if (contract is null)
        {
            return null;
        }
        return ConvertToDto(contract);
    }

    public async Task<ContractBillingTimelineDto?> GetBillingTimelineAsync(
        int contractId,
        int userId,
        string role,
        CancellationToken cancellationToken)
    {
        var (customerId, managerId) = GetScope(userId, role);
        var contractReadModel = await _repository.GetByIdAsync(
            contractId,
            customerId,
            managerId);
        if (contractReadModel is null)
        {
            if ((customerId.HasValue || managerId.HasValue) &&
                await _repository.ExistsAsync(contractId))
            {
                throw new ContractAuthorizationException("You cannot access this contract.");
            }

            return null;
        }

        var today = TodayInBusinessTimeZone();
        var issuedInvoices = await _repository.GetIssuedInvoicesForTimelineAsync(
            contractId,
            cancellationToken);
        var issuedMonths = issuedInvoices
            .Select(invoice => invoice.InvoiceMonth)
            .ToHashSet();
        var plannedPeriods = new List<PlannedBillingPeriodDto>();
        var currentMonth = FirstDayOfMonth(contractReadModel.StartDate);
        var endMonth = FirstDayOfMonth(contractReadModel.EndDate);
        var hasMorePlannedPeriods = false;

        while (contractReadModel.Status != ContractStatuses.Terminated && currentMonth <= endMonth)
        {
            if (!issuedMonths.Contains(currentMonth))
            {
                if (plannedPeriods.Count == MaximumPlannedPeriods)
                {
                    hasMorePlannedPeriods = true;
                    break;
                }

                plannedPeriods.Add(new PlannedBillingPeriodDto(
                    currentMonth,
                    contractReadModel.MonthlyRent,
                    "NotIssued",
                    null,
                    null,
                    null,
                    "Chưa có dữ liệu",
                    GetProrationNote(contractReadModel.StartDate, contractReadModel.EndDate, currentMonth)));
            }

            currentMonth = currentMonth.AddMonths(1);
        }

        var invoiceDtos = issuedInvoices
            .OrderBy(invoice => invoice.InvoiceMonth)
            .Select(invoice => ConvertToDto(invoice, today))
            .ToArray();

        return new ContractBillingTimelineDto(
            ConvertToDto(contractReadModel),
            invoiceDtos,
            plannedPeriods,
            hasMorePlannedPeriods);
    }

    public async Task<ContractDto> ExtendContractAsync(
        int contractId,
        ExtendContractRequestDto request,
        int userId,
        string role,
        CancellationToken cancellationToken)
    {
        var contract = await GetContractForManagerMutationAsync(contractId, userId, role);
        if (contract.Status == ContractStatuses.Terminated)
        {
            throw new ContractConflictException("A terminated contract cannot be extended.");
        }
        if (contract.Status != ContractStatuses.Active)
        {
            throw new ContractConflictException("Only active contracts can be extended.");
        }

        var today = TodayInBusinessTimeZone();
        if (request.NewEndDate <= contract.EndDate)
        {
            throw new ContractBusinessRuleException("The new end date must be later than the current end date.");
        }
        if (request.NewEndDate <= today)
        {
            throw new ContractBusinessRuleException("The new end date must be a future date.");
        }

        var notification = new Notifications
        {
            UserId = contract.CustomerId,
            Title = $"Hợp đồng phòng {contract.RoomCode} đã được gia hạn đến {request.NewEndDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
            Type = NotificationTypes.Contract,
            IsRead = false
        };
        var result = await _repository.ExtendAsync(
            contractId,
            userId,
            request.NewEndDate,
            today,
            _timeProvider.GetUtcNow(),
            notification,
            cancellationToken);
        if (result.Status == ContractMutationStatus.Conflict)
        {
            throw new ContractConflictException(
                "The contract, room, or requested extension changed or conflicts with another booking.");
        }

        var updatedContract = GetUpdatedContract(result);
        await PublishContractNotificationAsync(notification);
        return ConvertToDto(updatedContract);
    }

    public async Task<TerminateContractResponseDto> TerminateContractAsync(
        int contractId,
        TerminateContractRequestDto request,
        int userId,
        string role,
        CancellationToken cancellationToken)
    {
        if (!request.CheckoutConfirmed)
        {
            throw new ContractBusinessRuleException("Confirm that the customer has handed over the room.");
        }

        var contract = await GetContractForManagerMutationAsync(contractId, userId, role);
        if (contract.Status != ContractStatuses.Active)
        {
            throw new ContractConflictException("This contract has already ended or is not active.");
        }

        var notification = new Notifications
        {
            UserId = contract.CustomerId,
            Title = $"Hợp đồng phòng {contract.RoomCode} đã kết thúc và trả phòng",
            Type = NotificationTypes.Contract,
            IsRead = false
        };
        var result = await _repository.TerminateAsync(
            contractId,
            userId,
            request.RoomReadyForRental,
            request.OutstandingDebtAcknowledged,
            _timeProvider.GetUtcNow(),
            notification,
            cancellationToken);
        if (result.Status == ContractMutationStatus.Conflict)
        {
            if (result.OutstandingDebt > 0m && !request.OutstandingDebtAcknowledged)
            {
                throw new ContractBusinessRuleException(
                    "Acknowledge the outstanding invoice balance before confirming checkout.");
            }

            throw new ContractConflictException(
                "The contract, room, or another booking changed; refresh the contract and try again.");
        }

        var terminatedContract = GetUpdatedContract(result);
        await PublishContractNotificationAsync(notification);
        return new TerminateContractResponseDto(
            ConvertToDto(terminatedContract),
            result.OutstandingDebt);
    }

    public async Task MarkTerminatedRoomReadyAsync(
        int contractId,
        int userId,
        string role,
        CancellationToken cancellationToken)
    {
        var contract = await GetContractForManagerMutationAsync(contractId, userId, role);
        if (contract.Status != ContractStatuses.Terminated)
        {
            throw new ContractConflictException("Only a terminated contract can release its room after cleanup.");
        }

        var result = await _repository.MarkTerminatedRoomReadyAsync(
            contractId,
            userId,
            _timeProvider.GetUtcNow(),
            cancellationToken);
        switch (result)
        {
            case ContractMutationStatus.Updated:
                return;
            case ContractMutationStatus.NotFound:
                throw new ContractNotFoundException("Contract does not exist.");
            case ContractMutationStatus.Forbidden:
                throw new ContractAuthorizationException(
                    "You are no longer assigned to manage this property's contracts.");
            default:
                throw new ContractConflictException(
                    "The room still has another booking or contract, or is not ready to be released.");
        }
    }

    private async Task<ContractReadModel> GetContractForManagerMutationAsync(
        int contractId,
        int userId,
        string role)
    {
        if (role != RoleNames.Manager)
        {
            throw new ContractAuthorizationException("Only an assigned Manager can change a contract.");
        }

        var contract = await _repository.GetByIdAsync(contractId, null, null);
        if (contract is null)
        {
            throw new ContractNotFoundException("Contract does not exist.");
        }
        if (!await _repository.HasActiveManagerAssignmentAsync(contract.PropertyId, userId))
        {
            throw new ContractAuthorizationException("You are not assigned to manage this property's contracts.");
        }

        return contract;
    }

    private static ContractReadModel GetUpdatedContract(ContractMutationResult result)
    {
        return result.Status switch
        {
            ContractMutationStatus.Updated when result.Contract is not null => result.Contract,
            ContractMutationStatus.NotFound => throw new ContractNotFoundException("Contract does not exist."),
            ContractMutationStatus.Forbidden => throw new ContractAuthorizationException(
                "You are no longer assigned to manage this property's contracts."),
            _ => throw new ContractConflictException("The contract could not be updated.")
        };
    }

    private async Task PublishContractNotificationAsync(Notifications notification)
    {
        if (_realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync([notification]);
        }
    }

    private static Contracts ConvertToEntity(CreateContractDto dto, ContractBookingReadModel booking, int userId)
    {
        return new Contracts
        {
            RoomId = booking.RoomId,
            CustomerId = booking.CustomerId,
            BookingId = booking.BookingId,
            ProcessedByStaffId = userId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            MonthlyRent = booking.MonthlyRent!.Value,
            DepositAmount = booking.DepositAmount,
            Status = ContractStatuses.Active
        };
    }

    private static (int? CustomerId, int? ManagerId) GetScope(int userId, string role)
    {
        var scope = AccessScope.For(userId, role);
        if (scope is null)
        {
            throw new ContractAuthorizationException("Your role cannot access contracts.");
        }
        return scope.Value;
    }

    private ContractDto ConvertToDto(ContractReadModel contract)
    {
        return new ContractDto(
            contract.ContractId,
            contract.BookingId,
            contract.RoomId,
            contract.RoomCode,
            contract.PropertyId,
            contract.PropertyName,
            contract.CustomerId,
            contract.CustomerName,
            contract.CustomerPhone,
            contract.StartDate,
            contract.EndDate,
            contract.MonthlyRent,
            contract.DepositAmount,
            contract.Status,
            contract.ProcessedByStaffName,
            contract.PrepaidRentAmount,
            contract.PrepaidRentApplied,
            contract.RemainingPrepaidCredit,
            contract.PropertyAddress,
            contract.LessorUserId,
            contract.LessorName,
            contract.LessorAddress,
            GetDisplayStatus(contract.Status, contract.StartDate, contract.EndDate),
            contract.InitialElectricReading,
            contract.InitialWaterReading,
            contract.MeterHandoverDate,
            contract.HasUtilityReadings,
            contract.HasInvoices);
    }

    private static InvoiceDto ConvertToDto(InvoiceReadModel invoice, DateOnly today)
    {
        var displayStatus = invoice.Status switch
        {
            InvoiceStatuses.Paid => "Paid",
            InvoiceStatuses.Overdue => "Overdue",
            InvoiceStatuses.Unpaid when invoice.DueDate < today => "Overdue",
            _ => "Unpaid"
        };

        return new InvoiceDto(
            invoice.InvoiceId,
            invoice.ContractId,
            invoice.RoomCode,
            invoice.PropertyName,
            invoice.CustomerName,
            invoice.InvoiceMonth,
            invoice.DueDate,
            invoice.Status,
            invoice.Total,
            invoice.Lines.Select(line => new InvoiceLineDto(
                line.ItemType,
                line.Description,
                line.Quantity,
                line.UnitPrice,
                line.UnitCode,
                line.Amount)).ToArray(),
            invoice.PrepaidRentApplied,
            invoice.NetPayable,
            invoice.AmountPaid,
            invoice.RemainingPayable,
            invoice.PaymentMethod,
            invoice.PaymentDateUtc,
            displayStatus);
    }

    private static DateOnly FirstDayOfMonth(DateOnly date)
    {
        return new DateOnly(date.Year, date.Month, 1);
    }

    private static string? GetProrationNote(
        DateOnly startDate,
        DateOnly endDate,
        DateOnly invoiceMonth)
    {
        var isFirstMonth = invoiceMonth.Year == startDate.Year && invoiceMonth.Month == startDate.Month;
        var isFinalMonth = invoiceMonth.Year == endDate.Year && invoiceMonth.Month == endDate.Month;
        if ((isFirstMonth && startDate.Day != 1) ||
            (isFinalMonth && endDate.Day != DateTime.DaysInMonth(endDate.Year, endDate.Month)))
        {
            return "Chính sách tính tiền khi bắt đầu/kết thúc giữa tháng chưa hỗ trợ prorate.";
        }

        return null;
    }

    private DateOnly TodayInBusinessTimeZone()
    {
        return DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
    }

    private string GetDisplayStatus(string status, DateOnly startDate, DateOnly endDate)
    {
        if (status == ContractStatuses.Terminated)
        {
            return ContractStatuses.Terminated;
        }
        if (status != ContractStatuses.Active)
        {
            return "Ended";
        }

        var today = TodayInBusinessTimeZone();
        if (today < startDate)
        {
            return "Upcoming";
        }

        return today <= endDate ? "Active" : "ExpiredPendingCheckout";
    }
}

public sealed class ContractNotFoundException(string message) : Exception(message);

public sealed class ContractAuthorizationException(string message) : Exception(message);

public sealed class ContractBusinessRuleException(string message) : Exception(message);

public sealed class ContractConflictException(string message) : Exception(message);
