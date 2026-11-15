using System.Globalization;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Application.Validation;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

// Rebuild 4.2 (PLAN.md D4, câu 13): chuyển người đứng tên khi người đứng tên chuyển đi, người ở cùng ở tiếp.
// Luồng: người đứng tên cũ hoặc quản lý tạo đề nghị → người cũ và người nhận xác nhận → quản lý hoàn tất.
// Hoàn tất = kết thúc hợp đồng cũ vào ngày trước ngày hiệu lực, tạo hợp đồng mới từ ngày hiệu lực với cùng tiền
// phòng, ngày kết thúc, cọc chuyển nguyên, chỉ số điện nước nối tiếp, biên bản kiểm tra thiết bị làm biên bản
// nhận phòng và những người ở cùng còn lại.
public sealed class ContractTransferService(
    IContractTransferRepository repository,
    IContractRepository contracts,
    TimeProvider clock,
    INotificationRealtimePublisher? realtimePublisher = null,
    IUserContactLookup? contactLookup = null,
    IAuditTrail? audit = null) : IContractTransferService
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public async Task<List<ContractTransferDto>> ListAsync(int userId, string role, CancellationToken cancellationToken)
    {
        var scope = AccessScope.For(userId, role)
            ?? throw new ContractAuthorizationException("Your role cannot view contract transfers.");
        var result = new List<ContractTransferDto>();
        foreach (var transfer in await repository.ListAsync(scope.CustomerId, scope.ManagerId, cancellationToken))
        {
            result.Add(await ToDtoAsync(transfer, userId, role, cancellationToken));
        }
        return result;
    }

    public async Task<ContractTransferDto> CreateAsync(CreateContractTransferDto dto, int userId, string role,
        CancellationToken cancellationToken)
    {
        var contract = role switch
        {
            RoleNames.Customer => await GetContractAsync(dto.ContractId, userId, null),
            RoleNames.Manager => await GetContractAsync(dto.ContractId, null, userId),
            _ => throw new ContractAuthorizationException("Chỉ người đứng tên hoặc quản lý phụ trách được đề nghị chuyển hợp đồng.")
        };
        if (contract.Status != ContractStatuses.Active)
        {
            throw new ContractConflictException("Chỉ hợp đồng đang hiệu lực mới chuyển người đứng tên được.");
        }

        var today = BookingRules.Today(clock);
        if (dto.EffectiveDate <= FirstOfMonth(contract.StartDate))
        {
            throw new ContractBusinessRuleException("Ngày hiệu lực phải từ ngày 1 của tháng sau tháng bắt đầu hợp đồng.");
        }
        if (dto.EffectiveDate < FirstOfMonth(today))
        {
            throw new ContractBusinessRuleException("Ngày hiệu lực không được trước tháng hiện tại.");
        }
        if (dto.EffectiveDate >= contract.EndDate)
        {
            throw new ContractBusinessRuleException($"Ngày hiệu lực phải trước ngày kết thúc hợp đồng ({Format(contract.EndDate)}).");
        }

        var holder = await repository.FindUserByEmailAsync(dto.NewHolderEmail.Trim(), cancellationToken);
        if (holder is null || holder.RoleName != RoleNames.Customer || !holder.IsActive)
        {
            throw new ContractBusinessRuleException(
                "Chưa có tài khoản khách đang hoạt động với email này. Hãy nhờ người ở lại đăng ký tài khoản khách trước.");
        }
        if (holder.UserId == contract.CustomerId)
        {
            throw new ContractBusinessRuleException("Người nhận phải khác người đang đứng tên hợp đồng.");
        }
        // Hợp đồng ghi số điện thoại của người đứng tên, giống khi lập hợp đồng từ đặt cọc.
        await PhoneNumberRequiredException.EnsureAsync(contactLookup, holder.UserId,
            "Người nhận chưa có số điện thoại. Hãy nhờ họ cập nhật số điện thoại trong trang cá nhân.");
        if (dto.OccupantId is { } occupantId &&
            await repository.GetActiveOccupantNameAsync(contract.ContractId, occupantId, cancellationToken) is null)
        {
            throw new ContractBusinessRuleException("Người ở cùng được chọn không còn ở trong phòng.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var transfer = new ContractTransfers
        {
            FromContractId = contract.ContractId,
            NewHolderId = holder.UserId,
            BecomingHolderOccupantId = dto.OccupantId,
            EffectiveDate = dto.EffectiveDate,
            Status = ContractTransferStatuses.Pending,
            RequestedById = userId,
            CreatedAtUtc = now,
            // Người đứng tên tự tạo đề nghị thì coi như đã đồng ý.
            OldHolderConfirmedAtUtc = role == RoleNames.Customer ? now : null
        };
        var notifications = new List<Notifications>
        {
            Notify(holder.UserId, contract.ContractId, $"Đề nghị nhận hợp đồng phòng {contract.RoomCode}",
                $"Bạn được đề nghị đứng tên hợp đồng phòng {contract.RoomCode} ({contract.PropertyName}) từ ngày {Format(dto.EffectiveDate)}. Vào \"Hợp đồng của tôi\" để xem và xác nhận.")
        };
        var others = role == RoleNames.Customer
            ? await contracts.GetActiveManagerIdsAsync(contract.PropertyId, cancellationToken)
            : [contract.CustomerId];
        notifications.AddRange(others.Select(id => Notify(id, contract.ContractId,
            $"Đề nghị chuyển người đứng tên phòng {contract.RoomCode}",
            $"Chuyển hợp đồng sang {holder.FullName} từ ngày {Format(dto.EffectiveDate)}.")));

        if (!await repository.AddAsync(transfer, notifications, cancellationToken))
        {
            throw new ContractConflictException("Hợp đồng này đang có một đề nghị chuyển chờ xử lý.");
        }
        await PublishAsync(notifications);
        return await GetDtoAsync(transfer.ContractTransferId, userId, role, cancellationToken);
    }

    public async Task<ContractTransferDto> ConfirmAsync(int transferId, int userId, string role, CancellationToken cancellationToken)
    {
        var transfer = await GetPendingAsync(transferId, cancellationToken);
        var asOld = role == RoleNames.Customer && userId == transfer.OldHolderId;
        var asNew = role == RoleNames.Customer && userId == transfer.NewHolderId;
        if (!asOld && !asNew)
        {
            throw new ContractAuthorizationException("Chỉ người đứng tên cũ và người nhận được xác nhận đề nghị này.");
        }
        if ((asOld ? transfer.OldHolderConfirmedAtUtc : transfer.NewHolderConfirmedAtUtc) is not null)
        {
            throw new ContractConflictException("Bạn đã xác nhận đề nghị này.");
        }

        var name = asOld ? transfer.OldHolderName : transfer.NewHolderName;
        var notifications = await NotifyPartiesAsync(transfer, userId, cancellationToken,
            $"{name} đã đồng ý chuyển hợp đồng phòng {transfer.RoomCode}",
            asOld ? "Người đứng tên cũ đồng ý chuyển hợp đồng và tiền cọc." : "Người nhận đồng ý đứng tên hợp đồng.");
        if (!await repository.ConfirmAsync(transferId, asOld, clock.GetUtcNow().UtcDateTime, notifications, cancellationToken))
        {
            throw new ContractConflictException("Đề nghị vừa thay đổi. Vui lòng tải lại.");
        }
        await PublishAsync(notifications);
        return await GetDtoAsync(transferId, userId, role, cancellationToken);
    }

    public async Task<ContractTransferDto> CancelAsync(int transferId, CancelContractTransferDto dto, int userId, string role,
        CancellationToken cancellationToken)
    {
        var transfer = await GetPendingAsync(transferId, cancellationToken);
        var isParty = role == RoleNames.Customer && (userId == transfer.OldHolderId || userId == transfer.NewHolderId);
        var isManager = role == RoleNames.Manager &&
            await contracts.HasActiveManagerAssignmentAsync(transfer.PropertyId, userId);
        if (!isParty && !isManager)
        {
            throw new ContractAuthorizationException("Bạn không thể hủy đề nghị này.");
        }

        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        var notifications = await NotifyPartiesAsync(transfer, userId, cancellationToken,
            $"Đã hủy đề nghị chuyển hợp đồng phòng {transfer.RoomCode}",
            reason is null ? "Hợp đồng giữ nguyên người đứng tên." : $"Lý do: {reason}");
        if (!await repository.CancelAsync(transferId, userId, clock.GetUtcNow().UtcDateTime, reason, notifications, cancellationToken))
        {
            throw new ContractConflictException("Đề nghị vừa thay đổi. Vui lòng tải lại.");
        }
        await PublishAsync(notifications);
        return await GetDtoAsync(transferId, userId, role, cancellationToken);
    }

    public async Task<ContractTransferDto> CompleteAsync(int transferId, int userId, string role, CancellationToken cancellationToken)
    {
        var transfer = await GetPendingAsync(transferId, cancellationToken);
        if (role != RoleNames.Manager || !await contracts.HasActiveManagerAssignmentAsync(transfer.PropertyId, userId))
        {
            throw new ContractAuthorizationException("Chỉ quản lý đang phụ trách khu trọ được hoàn tất chuyển hợp đồng.");
        }

        var contract = await GetContractAsync(transfer.FromContractId, null, null);
        var facts = await repository.GetFactsAsync(transfer.FromContractId, transfer.BecomingHolderOccupantId,
            transfer.EffectiveDate, cancellationToken);
        var blockers = await BlockersAsync(transfer, contract, facts, cancellationToken);
        if (blockers.Count > 0)
        {
            throw new ContractConflictException(string.Join(" ", blockers));
        }

        // Chỉ số điện nước nối tiếp: kỳ cuối của hợp đồng cũ là chỉ số đầu của hợp đồng mới.
        var (electric, water) = facts.LatestReadingMonth is not null
            ? (facts.LatestElectricEnd, facts.LatestWaterEnd)
            : facts.HasMeterBaseline ? (facts.InitialElectricReading, facts.InitialWaterReading) : (null, null);
        var notifications = new List<Notifications>
        {
            Notify(transfer.OldHolderId, contract.ContractId, $"Hợp đồng phòng {transfer.RoomCode} đã chuyển sang {transfer.NewHolderName}",
                $"Hợp đồng của bạn kết thúc ngày {Format(transfer.EffectiveDate.AddDays(-1))}. Tiền cọc {Money(contract.SecurityDepositAmount)} đã chuyển sang hợp đồng mới; phần cọc giữa hai bạn tự thanh toán với nhau."),
            Notify(transfer.NewHolderId, contract.ContractId, $"Bạn đã đứng tên hợp đồng phòng {transfer.RoomCode}",
                $"Hợp đồng mới có hiệu lực từ ngày {Format(transfer.EffectiveDate)} đến {Format(contract.EndDate)}, tiền phòng {Money(contract.MonthlyRent)}/tháng, tiền cọc đang giữ {Money(contract.SecurityDepositAmount)}.")
        };
        var newContractId = await repository.CompleteAsync(new ContractTransferCompletion(transferId, userId,
            clock.GetUtcNow().UtcDateTime, contract.SecurityDepositAmount, electric, water,
            contract.RegisteredVehicleCount, contract.ManualSecurityDepositAmount,
            contract.SecurityDepositAcknowledgedAtUtc, contract.SecurityDepositRecordedByManagerId), notifications, cancellationToken);
        if (newContractId is null)
        {
            throw new ContractConflictException("Đề nghị hoặc hợp đồng vừa thay đổi. Vui lòng tải lại.");
        }
        audit?.Record(
            new { ContractId = contract.ContractId, HolderId = transfer.OldHolderId, contract.Status, contract.EndDate },
            new
            {
                ContractId = newContractId, HolderId = transfer.NewHolderId, transfer.EffectiveDate,
                OldContractEndDate = transfer.EffectiveDate.AddDays(-1), DepositCarriedOver = contract.SecurityDepositAmount
            });
        await PublishAsync(notifications);
        return await GetDtoAsync(transferId, userId, role, cancellationToken);
    }

    // Mọi điều kiện để hoàn tất. Dùng chung cho việc chặn hoàn tất và cho danh sách hiển thị cho quản lý.
    private async Task<List<string>> BlockersAsync(ContractTransferReadModel transfer, ContractReadModel contract,
        ContractTransferFactsReadModel facts, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();
        var lastMonth = transfer.EffectiveDate.AddMonths(-1);
        if (contract.Status != ContractStatuses.Active)
        {
            blockers.Add("Hợp đồng cũ không còn hiệu lực.");
            return blockers;
        }
        if (transfer.OldHolderConfirmedAtUtc is null)
        {
            blockers.Add("Người đứng tên cũ chưa xác nhận.");
        }
        if (transfer.NewHolderConfirmedAtUtc is null)
        {
            blockers.Add("Người nhận chưa xác nhận.");
        }
        if (BookingRules.Today(clock) < transfer.EffectiveDate)
        {
            blockers.Add($"Chưa tới ngày hiệu lực ({Format(transfer.EffectiveDate)}).");
        }
        if (facts.HasReadingFromEffectiveMonth || facts.HasInvoiceFromEffectiveMonth)
        {
            blockers.Add($"Hợp đồng cũ đã được chốt điện nước hoặc lập hóa đơn cho tháng {MonthText(transfer.EffectiveDate)} trở đi; các tháng này thuộc hợp đồng mới.");
        }
        if (facts.HasMeterBaseline && facts.LatestReadingMonth != lastMonth)
        {
            blockers.Add($"Cần chốt chỉ số điện nước tháng {MonthText(lastMonth)} cho hợp đồng cũ.");
        }
        if (!facts.HasLastMonthInvoice)
        {
            blockers.Add($"Cần lập hóa đơn tháng {MonthText(lastMonth)} cho hợp đồng cũ.");
        }
        var debt = (await contracts.GetIssuedInvoicesForTimelineAsync(contract.ContractId, cancellationToken))
            .Sum(invoice => invoice.RemainingPayable);
        if (debt > 0m)
        {
            blockers.Add($"Hợp đồng cũ còn nợ hóa đơn {Money(debt)}.");
        }
        if (!facts.HasMoveOutRecord)
        {
            blockers.Add("Cần lập biên bản kiểm tra thiết bị (biên bản trả phòng của hợp đồng cũ).");
        }
        else if (facts.MoveOutHasDamagedOrMissing)
        {
            blockers.Add("Biên bản có thiết bị hư hỏng hoặc mất nên không chuyển cọc được. Hãy trả phòng thường (quyết toán cọc) để người mới đặt cọc mới.");
        }
        if (facts.IsDepositSettled)
        {
            blockers.Add("Hợp đồng cũ đã quyết toán cọc nên không còn cọc để chuyển.");
        }
        if (transfer.BecomingHolderOccupantId is not null && !facts.BecomingHolderIsActive)
        {
            blockers.Add("Người ở cùng được chọn đã dọn ra.");
        }
        // Người đứng tên cũ rời đi; người nhận đứng tên (nếu họ đang là người ở cùng thì không tính hai lần).
        var afterTransfer = 1 + facts.ActiveOccupants - (facts.BecomingHolderIsActive ? 1 : 0);
        if (afterTransfer > facts.RoomMaxOccupants)
        {
            blockers.Add($"Sau khi chuyển phòng sẽ có {afterTransfer} người, vượt tối đa {facts.RoomMaxOccupants} người.");
        }
        return blockers;
    }

    private async Task<ContractTransferDto> GetDtoAsync(int transferId, int userId, string role, CancellationToken cancellationToken) =>
        await ToDtoAsync((await repository.GetAsync(transferId, cancellationToken))!, userId, role, cancellationToken);

    private async Task<ContractTransferDto> ToDtoAsync(ContractTransferReadModel transfer, int userId, string role,
        CancellationToken cancellationToken)
    {
        var pending = transfer.Status == ContractTransferStatuses.Pending;
        var contract = await GetContractAsync(transfer.FromContractId, null, null);
        // Đã hoàn tất: tiền phòng, ngày kết thúc và cọc lấy theo hợp đồng mới.
        var shown = transfer.NewContractId is { } newId ? await GetContractAsync(newId, null, null) : contract;
        var staff = role is RoleNames.Manager or RoleNames.Admin;
        var blockers = pending && staff
            ? await BlockersAsync(transfer, contract, await repository.GetFactsAsync(transfer.FromContractId,
                transfer.BecomingHolderOccupantId, transfer.EffectiveDate, cancellationToken), cancellationToken)
            : [];
        var asOld = role == RoleNames.Customer && userId == transfer.OldHolderId;
        var asNew = role == RoleNames.Customer && userId == transfer.NewHolderId;
        return new ContractTransferDto(
            transfer.ContractTransferId, transfer.Status, transfer.FromContractId, transfer.RoomCode, transfer.PropertyName,
            transfer.OldHolderId, transfer.OldHolderName, transfer.NewHolderId, transfer.NewHolderName, transfer.NewHolderEmail,
            transfer.BecomingHolderOccupantName, transfer.EffectiveDate, shown.MonthlyRent, shown.EndDate,
            shown.SecurityDepositAmount, transfer.RequestedByName, UtcTimestamp.ForResponse(transfer.CreatedAtUtc),
            UtcTimestamp.ForResponse(transfer.OldHolderConfirmedAtUtc), UtcTimestamp.ForResponse(transfer.NewHolderConfirmedAtUtc),
            UtcTimestamp.ForResponse(transfer.CompletedAtUtc), transfer.NewContractId,
            UtcTimestamp.ForResponse(transfer.CancelledAtUtc), transfer.CancelReason,
            pending && ((asOld && transfer.OldHolderConfirmedAtUtc is null) || (asNew && transfer.NewHolderConfirmedAtUtc is null)),
            pending && (asOld || asNew || role == RoleNames.Manager),
            pending && role == RoleNames.Manager && blockers.Count == 0,
            blockers);
    }

    private async Task<ContractTransferReadModel> GetPendingAsync(int transferId, CancellationToken cancellationToken)
    {
        var transfer = await repository.GetAsync(transferId, cancellationToken)
            ?? throw new ContractNotFoundException("Đề nghị chuyển hợp đồng không tồn tại.");
        if (transfer.Status != ContractTransferStatuses.Pending)
        {
            throw new ContractConflictException("Đề nghị này không còn chờ xử lý.");
        }
        return transfer;
    }

    private async Task<ContractReadModel> GetContractAsync(int contractId, int? customerId, int? managerId) =>
        await contracts.GetByIdAsync(contractId, customerId, managerId)
            ?? throw (await contracts.ExistsAsync(contractId)
                ? new ContractAuthorizationException("You cannot access this contract.")
                : new ContractNotFoundException("Contract does not exist."));

    // Báo cho người cũ, người nhận và các quản lý phụ trách, trừ chính người vừa thao tác.
    private async Task<List<Notifications>> NotifyPartiesAsync(ContractTransferReadModel transfer, int actorId,
        CancellationToken cancellationToken, string title, string body)
    {
        var recipients = new HashSet<int> { transfer.OldHolderId, transfer.NewHolderId };
        recipients.UnionWith(await contracts.GetActiveManagerIdsAsync(transfer.PropertyId, cancellationToken));
        recipients.Remove(actorId);
        return recipients.Select(id => Notify(id, transfer.FromContractId, title, body)).ToList();
    }

    private static Notifications Notify(int userId, int contractId, string title, string body) => new()
    {
        UserId = userId,
        Title = title,
        Body = body,
        Type = NotificationTypes.Contract,
        RelatedEntityId = contractId,
        IsRead = false
    };

    private async Task PublishAsync(List<Notifications> notifications)
    {
        if (realtimePublisher is not null && notifications.Count > 0)
        {
            await realtimePublisher.PublishCreatedAsync(notifications);
        }
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
    private static string Format(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    private static string MonthText(DateOnly date) => date.ToString("MM/yyyy", CultureInfo.InvariantCulture);
    private static string Money(decimal amount) => $"{amount.ToString("#,0", Vietnamese)}đ";
}
