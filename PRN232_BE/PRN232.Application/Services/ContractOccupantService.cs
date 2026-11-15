using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Application.Validation;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

// Rebuild 4.1 (PLAN.md D4). Nhóm đã chốt:
// - CCCD người ở cùng bắt buộc (chủ trọ đăng ký tạm trú), mã hóa khi lưu, API danh sách chỉ trả số đã che.
// - Người đứng tên và quản lý phụ trách đều thêm người / ghi ngày dọn ra được, không cần duyệt (luật sức chứa
//   tự kiểm tra); người đứng tên thêm thì quản lý nhận thông báo và ngược lại.
// - Chỉ quản lý phụ trách và Admin là chủ khu trọ được xem đủ số CCCD ("con mắt"); mỗi lần xem là một request
//   POST nên được AuditLogFilter ghi nhật ký. Người đứng tên không xem lại được CCCD của người ở cùng.
// - Người ở cùng chuyển đi: tiền phòng và tiền cọc không đổi (bảng D4).
public sealed class ContractOccupantService(
    IContractOccupantRepository repository,
    IContractRepository contracts,
    TimeProvider clock,
    INotificationRealtimePublisher? realtimePublisher = null,
    ILogger<ContractOccupantService>? logger = null) : IContractOccupantService
{
    public const int MoveInWindowDays = 14;
    private readonly ILogger _logger = logger ?? NullLogger<ContractOccupantService>.Instance;

    public async Task<ContractOccupantsDto> GetAsync(int contractId, int userId, string role, CancellationToken cancellationToken)
    {
        var scope = AccessScope.For(userId, role)
            ?? throw new ContractAuthorizationException("Your role cannot view contract occupants.");
        var contract = await GetContractAsync(contractId, scope.CustomerId, scope.ManagerId);
        return await BuildAsync(contract, userId, role, cancellationToken);
    }

    public async Task<ContractOccupantsDto> AddAsync(int contractId, AddContractOccupantDto dto, int userId, string role,
        CancellationToken cancellationToken)
    {
        var contract = await GetContractForPartyAsync(contractId, userId, role);
        var today = BookingRules.Today(clock);
        if (dto.MoveInDate < contract.StartDate || dto.MoveInDate > contract.EndDate)
        {
            throw new ContractBusinessRuleException(
                $"Ngày dọn vào phải trong thời hạn hợp đồng ({Format(contract.StartDate)} – {Format(contract.EndDate)}).");
        }
        if (dto.MoveInDate > today.AddDays(MoveInWindowDays))
        {
            throw new ContractBusinessRuleException($"Ngày dọn vào tối đa {MoveInWindowDays} ngày kể từ hôm nay.");
        }

        var identity = dto.IdentityNumber.Trim();
        var current = await repository.GetAsync(contractId, cancellationToken);
        if (current.Any(occupant => occupant.MoveOutDate is null && occupant.IdentityNumber == identity))
        {
            throw new ContractBusinessRuleException("Người có số CCCD này đang ở trong phòng.");
        }

        var maxOccupants = await repository.GetRoomMaxOccupantsAsync(contract.RoomId, cancellationToken);
        var name = dto.FullName.Trim();
        var notifications = await NotifyOtherPartyAsync(contract, role, cancellationToken,
            $"Thêm người ở cùng phòng {contract.RoomCode}: {name}",
            $"Dọn vào ngày {Format(dto.MoveInDate)}.");
        var occupant = new ContractOccupants
        {
            ContractId = contractId,
            FullName = name,
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : AccountRules.NormalizePhone(dto.PhoneNumber),
            IdentityNumber = identity,
            MoveInDate = dto.MoveInDate,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            AddedById = userId,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        // Người đứng tên chiếm 1 chỗ.
        if (!await repository.AddAsync(occupant, maxOccupants - 1, notifications, cancellationToken))
        {
            throw new ContractBusinessRuleException(
                $"{BookingRules.OccupancyExceeded(contract.RoomCode, maxOccupants)} Hãy ghi ngày dọn ra cho người đã chuyển đi trước.");
        }
        await PublishAsync(notifications);
        return await BuildAsync(contract, userId, role, cancellationToken);
    }

    public async Task<ContractOccupantsDto> RecordMoveOutAsync(int contractId, int occupantId, RecordOccupantMoveOutDto dto,
        int userId, string role, CancellationToken cancellationToken)
    {
        var contract = await GetContractForPartyAsync(contractId, userId, role);
        var occupant = (await repository.GetAsync(contractId, cancellationToken))
            .SingleOrDefault(item => item.ContractOccupantId == occupantId)
            ?? throw new ContractNotFoundException("Người ở cùng không tồn tại trong hợp đồng này.");
        if (occupant.MoveOutDate is not null)
        {
            throw new ContractConflictException("Người này đã được ghi ngày dọn ra.");
        }
        if (dto.MoveOutDate < occupant.MoveInDate || dto.MoveOutDate > BookingRules.Today(clock) || dto.MoveOutDate > contract.EndDate)
        {
            throw new ContractBusinessRuleException("Ngày dọn ra phải từ ngày dọn vào đến hôm nay và không sau ngày kết thúc hợp đồng.");
        }

        var notifications = await NotifyOtherPartyAsync(contract, role, cancellationToken,
            $"{occupant.FullName} đã dọn ra khỏi phòng {contract.RoomCode}",
            $"Ngày dọn ra {Format(dto.MoveOutDate)}. Tiền phòng và tiền cọc của hợp đồng không thay đổi.");
        if (!await repository.RecordMoveOutAsync(contractId, occupantId, dto.MoveOutDate, notifications, cancellationToken))
        {
            throw new ContractConflictException("Người này đã được ghi ngày dọn ra.");
        }
        await PublishAsync(notifications);
        return await BuildAsync(contract, userId, role, cancellationToken);
    }

    public async Task<RevealedIdentityDto> RevealIdentityAsync(int contractId, int occupantId, int userId, string role,
        CancellationToken cancellationToken)
    {
        var contract = role switch
        {
            RoleNames.Manager => await GetContractAsync(contractId, null, userId),
            RoleNames.Admin => await GetContractAsync(contractId, null, null),
            _ => throw new ContractAuthorizationException("Chỉ quản lý hoặc chủ trọ được xem đầy đủ số CCCD.")
        };
        if (!CanReveal(contract, userId, role))
        {
            throw new ContractAuthorizationException("Chỉ chủ trọ của khu này được xem đầy đủ số CCCD.");
        }
        var occupant = (await repository.GetAsync(contractId, cancellationToken))
            .SingleOrDefault(item => item.ContractOccupantId == occupantId)
            ?? throw new ContractNotFoundException("Người ở cùng không tồn tại trong hợp đồng này.");
        // AuditLogFilter đã ghi lần xem này (ai, lúc nào, hợp đồng nào); log kỹ thuật KHÔNG chứa số CCCD.
        _logger.LogInformation("User {UserId} ({Role}) revealed the identity number of occupant {OccupantId} on contract {ContractId}.",
            userId, role, occupantId, contractId);
        return new RevealedIdentityDto(occupant.ContractOccupantId, occupant.FullName, occupant.IdentityNumber);
    }

    // "079201001234" -> "********1234". Mọi chỗ trả CCCD trong danh sách đều đi qua đây.
    public static string Mask(string identityNumber) =>
        identityNumber.Length <= 4
            ? new string('*', identityNumber.Length)
            : new string('*', identityNumber.Length - 4) + identityNumber[^4..];

    private static bool CanReveal(ContractReadModel contract, int userId, string role) =>
        role == RoleNames.Manager || (role == RoleNames.Admin && contract.LessorUserId == userId);

    private async Task<ContractOccupantsDto> BuildAsync(ContractReadModel contract, int userId, string role,
        CancellationToken cancellationToken)
    {
        var occupants = await repository.GetAsync(contract.ContractId, cancellationToken);
        var maxOccupants = await repository.GetRoomMaxOccupantsAsync(contract.RoomId, cancellationToken);
        return new ContractOccupantsDto(
            contract.ContractId,
            contract.CustomerName,
            maxOccupants,
            1 + occupants.Count(occupant => occupant.MoveOutDate is null),
            contract.Status == ContractStatuses.Active && role is RoleNames.Customer or RoleNames.Manager,
            CanReveal(contract, userId, role),
            occupants.Select(occupant => new ContractOccupantDto(occupant.ContractOccupantId, occupant.FullName,
                occupant.PhoneNumber, Mask(occupant.IdentityNumber), occupant.MoveInDate, occupant.MoveOutDate,
                occupant.MoveOutDate is null, occupant.Note, occupant.AddedByName,
                UtcTimestamp.ForResponse(occupant.CreatedAtUtc))).ToList());
    }

    // Người đứng tên của hợp đồng hoặc quản lý đang phụ trách; hợp đồng phải còn hiệu lực.
    private async Task<ContractReadModel> GetContractForPartyAsync(int contractId, int userId, string role)
    {
        var contract = role switch
        {
            RoleNames.Customer => await GetContractAsync(contractId, userId, null),
            RoleNames.Manager => await GetContractAsync(contractId, null, userId),
            _ => throw new ContractAuthorizationException("Chỉ người đứng tên hoặc quản lý phụ trách được cập nhật người ở cùng.")
        };
        if (contract.Status != ContractStatuses.Active)
        {
            throw new ContractConflictException("Chỉ hợp đồng đang hiệu lực mới cập nhật được người ở cùng.");
        }
        return contract;
    }

    private async Task<ContractReadModel> GetContractAsync(int contractId, int? customerId, int? managerId) =>
        await contracts.GetByIdAsync(contractId, customerId, managerId)
            ?? throw (await contracts.ExistsAsync(contractId)
                ? new ContractAuthorizationException("You cannot access this contract's occupants.")
                : new ContractNotFoundException("Contract does not exist."));

    // Người đứng tên làm thì báo quản lý; quản lý làm thì báo người đứng tên.
    private async Task<List<Notifications>> NotifyOtherPartyAsync(ContractReadModel contract, string role,
        CancellationToken cancellationToken, string title, string body)
    {
        var recipients = role == RoleNames.Customer
            ? await contracts.GetActiveManagerIdsAsync(contract.PropertyId, cancellationToken)
            : [contract.CustomerId];
        return recipients.Select(recipient => new Notifications
        {
            UserId = recipient,
            Title = title,
            Body = body,
            Type = NotificationTypes.Contract,
            RelatedEntityId = contract.ContractId,
            IsRead = false
        }).ToList();
    }

    private async Task PublishAsync(List<Notifications> notifications)
    {
        if (realtimePublisher is not null && notifications.Count > 0)
        {
            await realtimePublisher.PublishCreatedAsync(notifications);
        }
    }

    private static string Format(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
