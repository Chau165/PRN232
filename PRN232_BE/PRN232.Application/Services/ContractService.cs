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
    private readonly IDepositSettlementRepository? _settlements;
    private readonly IAuditTrail? _audit;

    public ContractService(
        IContractRepository repository,
        TimeProvider timeProvider,
        INotificationRealtimePublisher? realtimePublisher = null,
        ILogger<ContractService>? logger = null,
        IUserContactLookup? contactLookup = null,
        IDepositSettlementRepository? settlements = null,
        IAuditTrail? audit = null)
    {
        _audit = audit;
        _contactLookup = contactLookup;
        _settlements = settlements;
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
        _audit?.Record(null, new
        {
            created.BookingId, created.RoomCode, created.CustomerName, created.StartDate, created.EndDate,
            created.MonthlyRent, created.SecurityDepositAmount, created.Status
        });
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
        // Tháng cuối: tháng của ngày kết thúc, hoặc của ngày trả phòng đã báo nếu sớm hơn (giống hóa đơn).
        var lastBilledDay = RentProration.LastBilledDay(contractReadModel.EndDate, contractReadModel.PlannedMoveOutDate);
        var endMonth = FirstDayOfMonth(lastBilledDay);
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

                var (days, daysInMonth) = RentProration.BilledDays(currentMonth, contractReadModel.StartDate, lastBilledDay);
                plannedPeriods.Add(new PlannedBillingPeriodDto(
                    currentMonth,
                    RentProration.Rent(contractReadModel.MonthlyRent, days, daysInMonth),
                    "NotIssued",
                    null,
                    null,
                    null,
                    "Chưa có dữ liệu",
                    days < daysInMonth
                        ? $"Tháng không trọn: tiền phòng tính {days}/{daysInMonth} ngày."
                        : null));
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
        // Rebuild 3.6b: hợp đồng giữ cọc đảm bảo phải quyết toán cọc trước khi trả phòng (tiền cọc phải có chỗ đi).
        if (contract.SecurityDepositAmount > 0m && _settlements is not null &&
            !await _settlements.ExistsAsync(contractId, cancellationToken))
        {
            throw new ContractConflictException("Cần lập biên bản trả phòng và quyết toán cọc trước khi xác nhận trả phòng.");
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
        _audit?.Record(
            new { contract.Status, contract.EndDate },
            new { terminatedContract.Status, terminatedContract.EndDate, result.OutstandingDebt, request.RoomReadyForRental });
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
            contract.HasInvoices,
            contract.SecurityDepositAmount,
            contract.PlannedMoveOutDate,
            UtcTimestamp.ForResponse(contract.MoveOutNoticeAtUtc),
            NoticeDays(contract),
            ForfeitsDeposit(contract.PlannedMoveOutDate, contract.EndDate, NoticeDays(contract)),
            contract.TransferredFromContractId,
            contract.TransferredToContractId);
    }

    // Rebuild 3.6a (PLAN D3 bước 6). Trả phòng TRƯỚC ngày kết thúc hợp đồng mà báo trước dưới 30 ngày thì
    // mất phần cọc còn lại khi quyết toán; trả đúng/sau ngày kết thúc thì không cần báo trước.
    public const int MoveOutNoticeRequiredDays = 30;

    private static int? NoticeDays(ContractReadModel contract) =>
        contract is { PlannedMoveOutDate: { } planned, MoveOutNoticeAtUtc: { } noticeAt }
            ? planned.DayNumber - DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(noticeAt, DateTimeKind.Utc), BusinessTimeZone)).DayNumber
            : null;

    public static bool ForfeitsDeposit(DateOnly? moveOutDate, DateOnly endDate, int? noticeDays) =>
        moveOutDate is { } date && date < endDate && (noticeDays ?? 0) < MoveOutNoticeRequiredDays;

    public async Task<ContractDto> GiveMoveOutNoticeAsync(int contractId, MoveOutNoticeDto request, int userId,
        string role, CancellationToken cancellationToken)
    {
        var contract = await GetContractForPartyMutationAsync(contractId, userId, role);
        if (contract.Status != ContractStatuses.Active)
        {
            throw new ContractConflictException("Chỉ hợp đồng đang hiệu lực mới báo trả phòng được.");
        }
        var planned = request.PlannedMoveOutDate;
        if (planned < TodayInBusinessTimeZone())
        {
            throw new ContractBusinessRuleException("Ngày trả phòng không được ở quá khứ.");
        }
        if (planned > contract.EndDate)
        {
            throw new ContractBusinessRuleException(
                $"Ngày trả phòng không được sau ngày kết thúc hợp đồng ({FormatDate(contract.EndDate)}). Muốn ở lâu hơn hãy gia hạn hợp đồng.");
        }

        var noticeDays = planned.DayNumber - TodayInBusinessTimeZone().DayNumber;
        var warning = ForfeitsDeposit(planned, contract.EndDate, noticeDays)
            ? $" Trả phòng trước ngày kết thúc hợp đồng ({FormatDate(contract.EndDate)}) mà báo trước dưới {MoveOutNoticeRequiredDays} ngày thì mất phần tiền cọc còn lại khi quyết toán."
            : "";
        var notifications = await MoveOutNotificationsAsync(contract, role, cancellationToken,
            $"Báo trả phòng {contract.RoomCode} ngày {FormatDate(planned)}",
            $"Báo trước {noticeDays} ngày.{warning}");
        if (!await _repository.SaveMoveOutNoticeAsync(contractId, planned, _timeProvider.GetUtcNow().UtcDateTime, userId,
                notifications, cancellationToken))
        {
            throw new ContractConflictException("Hợp đồng vừa thay đổi. Vui lòng tải lại.");
        }
        _audit?.Record(
            new { contract.PlannedMoveOutDate },
            new { PlannedMoveOutDate = planned, NoticeDays = noticeDays, ForfeitsDeposit = warning.Length > 0 });
        return await ReloadAfterNoticeAsync(contractId, notifications);
    }

    public async Task<ContractDto> CancelMoveOutNoticeAsync(int contractId, int userId, string role,
        CancellationToken cancellationToken)
    {
        var contract = await GetContractForPartyMutationAsync(contractId, userId, role);
        if (contract.Status != ContractStatuses.Active || contract.PlannedMoveOutDate is null)
        {
            throw new ContractConflictException("Hợp đồng này không có báo trả phòng để hủy.");
        }
        var notifications = await MoveOutNotificationsAsync(contract, role, cancellationToken,
            $"Đã hủy báo trả phòng {contract.RoomCode} ngày {FormatDate(contract.PlannedMoveOutDate.Value)}",
            "Hợp đồng tiếp tục đến ngày kết thúc như cũ.");
        if (!await _repository.SaveMoveOutNoticeAsync(contractId, null, null, null, notifications, cancellationToken))
        {
            throw new ContractConflictException("Hợp đồng vừa thay đổi. Vui lòng tải lại.");
        }
        _audit?.Record(new { contract.PlannedMoveOutDate }, new { PlannedMoveOutDate = (DateOnly?)null });
        return await ReloadAfterNoticeAsync(contractId, notifications);
    }

    // Khách báo thì nhắn quản lý đang phụ trách; quản lý ghi nhận giúp thì nhắn khách.
    private async Task<List<Notifications>> MoveOutNotificationsAsync(ContractReadModel contract, string role,
        CancellationToken cancellationToken, string title, string body)
    {
        var recipients = role == RoleNames.Customer
            ? await _repository.GetActiveManagerIdsAsync(contract.PropertyId, cancellationToken)
            : [contract.CustomerId];
        return recipients.Select(userId => new Notifications
        {
            UserId = userId,
            Title = title,
            Body = body,
            Type = NotificationTypes.Contract,
            RelatedEntityId = contract.ContractId,
            IsRead = false
        }).ToList();
    }

    private async Task<ContractDto> ReloadAfterNoticeAsync(int contractId, List<Notifications> notifications)
    {
        if (_realtimePublisher is not null && notifications.Count > 0)
        {
            await _realtimePublisher.PublishCreatedAsync(notifications);
        }
        var updated = await _repository.GetByIdAsync(contractId, null, null)
            ?? throw new ContractNotFoundException("Contract does not exist.");
        return ConvertToDto(updated);
    }

    // Khách của chính hợp đồng hoặc quản lý đang phụ trách khu trọ.
    private async Task<ContractReadModel> GetContractForPartyMutationAsync(int contractId, int userId, string role)
    {
        if (role == RoleNames.Manager)
        {
            return await GetContractForManagerMutationAsync(contractId, userId, role);
        }
        if (role != RoleNames.Customer)
        {
            throw new ContractAuthorizationException("Chỉ khách thuê hoặc quản lý của hợp đồng được báo trả phòng.");
        }
        var contract = await _repository.GetByIdAsync(contractId, userId, null);
        if (contract is not null)
        {
            return contract;
        }
        throw await _repository.ExistsAsync(contractId)
            ? new ContractAuthorizationException("Bạn không phải khách thuê của hợp đồng này.")
            : new ContractNotFoundException("Contract does not exist.");
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

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
