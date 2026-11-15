using System.Globalization;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

// Rebuild 3.6b (PLAN.md D3 bước 6): quyết toán cọc lúc trả phòng. Thứ tự bắt buộc:
// biên bản trả phòng (3.5a) → quyết toán (ở đây) → trả phòng (ContractService.TerminateContractAsync).
// Ngày trả phòng lấy từ biên bản trả phòng; bồi thường = tổng tiền bồi thường trong biên bản đó.
public sealed class DepositSettlementService(
    IDepositSettlementRepository settlements,
    IContractRepository contracts,
    IHandoverRepository handovers,
    TimeProvider clock,
    INotificationRealtimePublisher? realtimePublisher = null,
    IAuditTrail? audit = null) : IDepositSettlementService
{
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public async Task<DepositSettlementDto> GetAsync(int contractId, int userId, string role, CancellationToken cancellationToken)
    {
        var scope = AccessScope.For(userId, role)
            ?? throw new ContractAuthorizationException("Your role cannot view deposit settlements.");
        var contract = await GetContractAsync(contractId, scope.CustomerId, scope.ManagerId);
        var stored = await settlements.GetAsync(contractId, cancellationToken);
        if (stored is not null)
        {
            return ToDto(stored, contract.EndDate);
        }
        var preview = await PreviewAsync(contract, cancellationToken);
        return ToDto(preview, contract.EndDate, isSettled: false);
    }

    public async Task<DepositSettlementDto> SettleAsync(int contractId, SettleDepositDto dto, int userId, string role,
        CancellationToken cancellationToken)
    {
        if (role != RoleNames.Manager)
        {
            throw new ContractAuthorizationException("Chỉ quản lý đang phụ trách khu trọ được quyết toán cọc.");
        }
        var contract = await GetContractAsync(contractId, null, userId);
        if (contract.Status != ContractStatuses.Active)
        {
            throw new ContractConflictException("Chỉ quyết toán cọc khi hợp đồng còn hiệu lực (trước khi trả phòng).");
        }
        if (await settlements.ExistsAsync(contractId, cancellationToken))
        {
            throw new ContractConflictException("Hợp đồng này đã được quyết toán cọc.");
        }

        var preview = await PreviewAsync(contract, cancellationToken);
        if (preview.OutstandingDebt > 0m && !dto.OutstandingDebtAcknowledged)
        {
            throw new ContractBusinessRuleException(
                $"Khách còn nợ hóa đơn {Money(preview.OutstandingDebt)}. Nên thu hết trước khi quyết toán; nếu khách không trả, hãy xác nhận để giữ lại cọc bù nợ.");
        }
        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        if (note is { Length: > 1000 })
        {
            throw new ContractBusinessRuleException("Ghi chú quyết toán tối đa 1000 ký tự.");
        }

        var settlement = new DepositSettlements
        {
            ContractId = contractId,
            MoveOutDate = preview.MoveOutDate,
            DepositAmount = preview.DepositAmount,
            DamageCharges = preview.DamageCharges,
            OutstandingDebt = preview.OutstandingDebt,
            RetainedForDebt = preview.RetainedForDebt,
            ForfeitedAmount = preview.ForfeitedAmount,
            RefundAmount = preview.RefundAmount,
            AmountDue = preview.AmountDue,
            EarlyWithoutNotice = preview.EarlyWithoutNotice,
            NoticeDays = preview.NoticeDays,
            Note = note,
            SettledById = userId,
            SettledAtUtc = clock.GetUtcNow().UtcDateTime
        };
        var notification = new Notifications
        {
            UserId = contract.CustomerId,
            Title = $"Quyết toán cọc phòng {contract.RoomCode}",
            Body = Summary(preview),
            Type = NotificationTypes.Contract,
            RelatedEntityId = contractId,
            IsRead = false
        };
        if (!await settlements.AddAsync(settlement, notification, cancellationToken))
        {
            throw new ContractConflictException("Hợp đồng vừa được quyết toán hoặc đã thay đổi. Vui lòng tải lại.");
        }
        if (realtimePublisher is not null)
        {
            await realtimePublisher.PublishCreatedAsync([notification]);
        }
        var settled = ToDto((await settlements.GetAsync(contractId, cancellationToken))!, contract.EndDate);
        audit?.Record(null, new
        {
            settled.DepositAmount, settled.DamageCharges, settled.OutstandingDebt, settled.RetainedForDebt,
            settled.ForfeitedAmount, settled.RefundAmount, settled.AmountDue, settled.EarlyWithoutNotice, settled.NoticeDays
        });
        return settled;
    }

    // Rebuild 3.6c: quản lý đã chuyển trả tiền cọc cho khách NGOÀI hệ thống và xác nhận để lưu vết.
    public Task<DepositSettlementDto> ConfirmRefundAsync(int contractId, int userId, string role,
        CancellationToken cancellationToken) =>
        ConfirmAsync(contractId, userId, role, cancellationToken,
            settlement => settlement.RefundAmount > 0m && settlement.RefundConfirmedAtUtc is null,
            "Không có khoản hoàn cọc cần xác nhận hoặc đã xác nhận trước đó.",
            (contract, settlement) => ($"Đã xác nhận hoàn {Money(settlement.RefundAmount)} tiền cọc phòng {contract.RoomCode}",
                $"Quản lý khu {contract.PropertyName} xác nhận đã chuyển lại cho bạn {Money(settlement.RefundAmount)} tiền cọc. Nếu bạn chưa nhận được, hãy liên hệ quản lý khu trọ."),
            settlements.ConfirmRefundAsync);

    // Rebuild 3.6c: khách đã nộp phần bồi thường vượt quá tiền cọc, quản lý xác nhận đã thu.
    public Task<DepositSettlementDto> ConfirmDueCollectedAsync(int contractId, int userId, string role,
        CancellationToken cancellationToken) =>
        ConfirmAsync(contractId, userId, role, cancellationToken,
            settlement => settlement.AmountDue > 0m && settlement.DueCollectedAtUtc is null,
            "Không có khoản khách cần nộp thêm hoặc đã xác nhận thu trước đó.",
            (contract, settlement) => ($"Đã xác nhận thu {Money(settlement.AmountDue)} tiền bồi thường phòng {contract.RoomCode}",
                $"Quản lý khu {contract.PropertyName} xác nhận đã nhận {Money(settlement.AmountDue)} tiền bồi thường thiết bị vượt quá tiền cọc. Quyết toán đã hoàn tất."),
            settlements.ConfirmDueCollectedAsync);

    private async Task<DepositSettlementDto> ConfirmAsync(int contractId, int userId, string role,
        CancellationToken cancellationToken,
        Func<DepositSettlementReadModel, bool> isPending,
        string nothingToConfirm,
        Func<ContractReadModel, DepositSettlementReadModel, (string Title, string Body)> message,
        Func<int, int, DateTime, Notifications, CancellationToken, Task<bool>> confirm)
    {
        // Giống "Đã hoàn cọc" ở booking (3.2b): quản lý đang phụ trách, hoặc Admin là chủ khu trọ.
        var contract = role switch
        {
            RoleNames.Manager => await GetContractAsync(contractId, null, userId),
            RoleNames.Admin => await GetContractAsync(contractId, null, null),
            _ => throw new ContractAuthorizationException("Chỉ quản lý hoặc chủ trọ được xác nhận khoản quyết toán.")
        };
        if (role == RoleNames.Admin && contract.LessorUserId != userId)
        {
            throw new ContractAuthorizationException("Chỉ chủ trọ của khu này được xác nhận khoản quyết toán.");
        }
        var settlement = await settlements.GetAsync(contractId, cancellationToken);
        if (settlement is null || !isPending(settlement))
        {
            throw new ContractConflictException(nothingToConfirm);
        }

        var (title, body) = message(contract, settlement);
        var notification = new Notifications
        {
            UserId = contract.CustomerId,
            Title = title,
            Body = body,
            Type = NotificationTypes.Contract,
            RelatedEntityId = contractId,
            IsRead = false
        };
        if (!await confirm(contractId, userId, clock.GetUtcNow().UtcDateTime, notification, cancellationToken))
        {
            throw new ContractConflictException(nothingToConfirm);
        }
        if (realtimePublisher is not null)
        {
            await realtimePublisher.PublishCreatedAsync([notification]);
        }
        var confirmed = ToDto((await settlements.GetAsync(contractId, cancellationToken))!, contract.EndDate);
        audit?.Record(
            new { settlement.RefundAmount, settlement.AmountDue, settlement.RefundConfirmedAtUtc, settlement.DueCollectedAtUtc },
            new { confirmed.RefundAmount, confirmed.AmountDue, confirmed.RefundConfirmedAtUtc, confirmed.DueCollectedAtUtc });
        return confirmed;
    }

    private async Task<ContractReadModel> GetContractAsync(int contractId, int? customerId, int? managerId) =>
        await contracts.GetByIdAsync(contractId, customerId, managerId)
            ?? throw (await contracts.ExistsAsync(contractId)
                ? new ContractAuthorizationException("You cannot access this contract's deposit settlement.")
                : new ContractNotFoundException("Contract does not exist."));

    private async Task<DepositSettlementReadModel> PreviewAsync(ContractReadModel contract, CancellationToken cancellationToken)
    {
        if (contract.SecurityDepositAmount <= 0m)
        {
            throw new ContractConflictException(
                "Hợp đồng này không có tiền cọc đảm bảo để quyết toán (theo điều khoản cũ, cọc đã trừ vào tiền phòng tháng đầu).");
        }
        var moveOut = (await handovers.GetRecordsAsync(contract.ContractId, cancellationToken))
            .SingleOrDefault(record => record.Kind == HandoverKinds.MoveOut)
            ?? throw new ContractConflictException("Cần lập biên bản trả phòng trước khi quyết toán cọc.");

        var damage = moveOut.Items.Sum(item => item.ChargeAmount);
        var debt = (await contracts.GetIssuedInvoicesForTimelineAsync(contract.ContractId, cancellationToken))
            .Sum(invoice => invoice.RemainingPayable);
        int? noticeDays = contract.MoveOutNoticeAtUtc is { } noticeAt
            ? moveOut.HandoverDate.DayNumber - DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(noticeAt, DateTimeKind.Utc), BusinessTimeZone)).DayNumber
            : null;
        var early = ContractService.ForfeitsDeposit(moveOut.HandoverDate, contract.EndDate, noticeDays);
        var result = DepositSettlementPolicy.Calculate(contract.SecurityDepositAmount, damage, debt, early);
        return new DepositSettlementReadModel(contract.ContractId, moveOut.HandoverDate, contract.SecurityDepositAmount,
            damage, debt, result.RetainedForDebt, result.Forfeited, result.Refund, result.AmountDue, early, noticeDays,
            null, "", default, null, null, null, null);
    }

    private static DepositSettlementDto ToDto(DepositSettlementReadModel model, DateOnly endDate, bool isSettled = true) => new(
        model.ContractId,
        isSettled,
        model.MoveOutDate,
        endDate,
        model.DepositAmount,
        model.DamageCharges,
        model.DamageCharges - model.AmountDue,
        model.OutstandingDebt,
        model.RetainedForDebt,
        model.ForfeitedAmount,
        model.RefundAmount,
        model.AmountDue,
        model.EarlyWithoutNotice,
        model.NoticeDays,
        model.Note,
        isSettled ? model.SettledByName : null,
        isSettled ? UtcTimestamp.ForResponse(model.SettledAtUtc) : null,
        UtcTimestamp.ForResponse(model.RefundConfirmedAtUtc),
        model.RefundConfirmedByName,
        UtcTimestamp.ForResponse(model.DueCollectedAtUtc),
        model.DueCollectedByName);

    // "Cọc 3.000.000đ: bồi thường thiết bị 1.200.000đ; hoàn lại 1.800.000đ."
    private static string Summary(DepositSettlementReadModel model)
    {
        var parts = new List<string>();
        if (model.DamageCharges > 0m) parts.Add($"bồi thường thiết bị {Money(model.DamageCharges - model.AmountDue)}");
        if (model.RetainedForDebt > 0m) parts.Add($"giữ lại bù nợ hóa đơn {Money(model.RetainedForDebt)}");
        if (model.ForfeitedAmount > 0m) parts.Add($"mất cọc do trả phòng trước hạn mà báo trước dưới {ContractService.MoveOutNoticeRequiredDays} ngày {Money(model.ForfeitedAmount)}");
        parts.Add($"hoàn lại {Money(model.RefundAmount)}");
        var due = model.AmountDue > 0m ? $" Bạn cần nộp thêm {Money(model.AmountDue)} tiền bồi thường vượt quá tiền cọc." : "";
        var refund = model.RefundAmount > 0m ? " Quản lý sẽ chuyển trả trực tiếp cho bạn (hệ thống không chuyển tiền)." : "";
        return $"Cọc {Money(model.DepositAmount)}: {string.Join("; ", parts)}.{due}{refund}";
    }

    private static string Money(decimal amount) => $"{amount.ToString("#,0", Vietnamese)}đ";
}
