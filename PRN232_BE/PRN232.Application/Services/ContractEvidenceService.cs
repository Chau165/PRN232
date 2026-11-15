using System.Buffers.Binary;
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

// Rebuild 3.5b/3.5c: ảnh/PDF minh chứng của hợp đồng. Nhóm đã chốt:
// - Khách và quản lý đang phụ trách cùng upload; KHÔNG có bước duyệt (bên kia trong tranh chấp không được
//   quyết định bằng chứng của mình). Mỗi người chỉ xóa file của mình, trong thời hạn upload.
// - Ảnh nhận phòng chỉ upload/xóa trong 7 ngày kể từ ngày nhận phòng (theo biên bản, chưa có thì ngày bắt
//   đầu hợp đồng). Ảnh trả phòng và bản hợp đồng đã ký: khi hợp đồng còn hiệu lực hoặc đã kết thúc.
// - Admin xem tất cả và chỉ ẩn được file không phù hợp (kèm lý do), không xóa.
public sealed class ContractEvidenceService(
    IContractEvidenceRepository repository,
    IHandoverRepository contracts,
    IContractEvidenceStorage storage,
    TimeProvider clock,
    ILogger<ContractEvidenceService>? logger = null,
    IDepositSettlementRepository? settlements = null) : IContractEvidenceService
{
    public const int MaxFilesPerKind = 20;
    public const int ImageMaxBytes = 5 * 1024 * 1024;
    public const int PdfMaxBytes = 10 * 1024 * 1024;
    public const int MoveInUploadDays = 7;
    private const int ReasonMaxLength = 500;

    private readonly ILogger _logger = logger ?? NullLogger<ContractEvidenceService>.Instance;

    public async Task<ContractEvidenceDto> GetAsync(int contractId, int userId, string role, CancellationToken cancellationToken)
    {
        var contract = await AuthorizeAsync(contractId, userId, role, cancellationToken);
        return await BuildAsync(contract, userId, role, cancellationToken);
    }

    public async Task<ContractEvidenceDto> UploadAsync(int contractId, string kind, ContractEvidenceUpload upload, int userId,
        string role, CancellationToken cancellationToken)
    {
        if (role is not (RoleNames.Customer or RoleNames.Manager))
        {
            throw new ContractAuthorizationException("Chỉ khách thuê và quản lý của hợp đồng được upload minh chứng.");
        }
        if (!ContractEvidenceKinds.IsValid(kind))
        {
            throw new ContractBusinessRuleException("Loại minh chứng phải là MoveIn, MoveOut hoặc SignedContract.");
        }
        var contract = await AuthorizeAsync(contractId, userId, role, cancellationToken);
        var deadline = await MoveInDeadlineAsync(contract, cancellationToken);
        if (!IsOpen(kind, contract, deadline))
        {
            throw new ContractConflictException(ClosedMessage(kind, deadline));
        }
        ValidateContent(upload);

        var objectPath = await storage.UploadAsync(contractId, kind, upload.ContentType, upload.Content);
        var file = new ContractEvidenceFiles
        {
            ContractId = contractId,
            Kind = kind,
            ObjectPath = objectPath,
            ContentType = upload.ContentType,
            SizeBytes = upload.Content.Length,
            FileName = CleanFileName(upload.FileName),
            UploadedById = userId,
            UploadedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        if (!await repository.AddAsync(file, MaxFilesPerKind, cancellationToken))
        {
            await DeleteObjectQuietlyAsync(objectPath);
            throw new ContractConflictException($"Mỗi loại minh chứng tối đa {MaxFilesPerKind} file.");
        }
        return await BuildAsync(contract, userId, role, cancellationToken);
    }

    public async Task<ContractEvidenceDto> DeleteAsync(int contractId, int fileId, int userId, string role,
        CancellationToken cancellationToken)
    {
        var contract = await AuthorizeAsync(contractId, userId, role, cancellationToken);
        var file = (await repository.GetFilesAsync(contractId, cancellationToken))
            .SingleOrDefault(item => item.ContractEvidenceFileId == fileId)
            ?? throw new ContractNotFoundException("File minh chứng không tồn tại.");
        if (file.UploadedById != userId)
        {
            throw new ContractAuthorizationException("Bạn chỉ xóa được file do chính mình upload.");
        }
        var deadline = await MoveInDeadlineAsync(contract, cancellationToken);
        if (!IsOpen(file.Kind, contract, deadline))
        {
            throw new ContractConflictException(ClosedMessage(file.Kind, deadline));
        }
        if (await repository.DeleteAsync(fileId, cancellationToken))
        {
            await DeleteObjectQuietlyAsync(file.ObjectPath);
        }
        return await BuildAsync(contract, userId, role, cancellationToken);
    }

    public async Task<ContractEvidenceDto> HideAsync(int contractId, int fileId, HideContractEvidenceDto dto, int userId,
        string role, CancellationToken cancellationToken)
    {
        if (role != RoleNames.Admin)
        {
            throw new ContractAuthorizationException("Chỉ Admin được ẩn file minh chứng.");
        }
        var reason = dto.Reason?.Trim() ?? "";
        if (reason.Length is 0 or > ReasonMaxLength)
        {
            throw new ContractBusinessRuleException($"Lý do ẩn là bắt buộc và tối đa {ReasonMaxLength} ký tự.");
        }
        var contract = await AuthorizeAsync(contractId, userId, role, cancellationToken);
        var file = (await repository.GetFilesAsync(contractId, cancellationToken))
            .SingleOrDefault(item => item.ContractEvidenceFileId == fileId)
            ?? throw new ContractNotFoundException("File minh chứng không tồn tại.");
        if (file.HiddenAtUtc is not null ||
            !await repository.HideAsync(fileId, userId, reason, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            throw new ContractConflictException("File này đã bị ẩn trước đó.");
        }
        return await BuildAsync(contract, userId, role, cancellationToken);
    }

    private async Task<HandoverContractReadModel> AuthorizeAsync(int contractId, int userId, string role,
        CancellationToken cancellationToken)
    {
        var contract = await contracts.GetContractAsync(contractId, cancellationToken)
            ?? throw new ContractNotFoundException("Contract does not exist.");
        var scope = AccessScope.For(userId, role)
            ?? throw new ContractAuthorizationException("Your role cannot access contract evidence.");
        if (scope.CustomerId is { } customerId && contract.CustomerId != customerId)
        {
            throw new ContractAuthorizationException("You cannot access another customer's contract evidence.");
        }
        if (scope.ManagerId is { } managerId &&
            !await contracts.HasActiveManagerAssignmentAsync(contract.PropertyId, managerId, cancellationToken))
        {
            throw new ContractAuthorizationException("You are not assigned to manage this property's contracts.");
        }
        return contract;
    }

    private sealed record Window(DateOnly MoveInDeadline, bool Settled);

    private async Task<Window> MoveInDeadlineAsync(HandoverContractReadModel contract, CancellationToken cancellationToken) => new(
        ((await repository.GetMoveInHandoverDateAsync(contract.ContractId, cancellationToken)) ?? contract.StartDate)
            .AddDays(MoveInUploadDays),
        settlements is not null && await settlements.ExistsAsync(contract.ContractId, cancellationToken));

    private bool IsOpen(string kind, HandoverContractReadModel contract, Window window) => kind switch
    {
        ContractEvidenceKinds.MoveIn => contract.Status == ContractStatuses.Active && BookingRules.Today(clock) <= window.MoveInDeadline,
        // Rebuild 3.6b: ảnh trả phòng là căn cứ bồi thường, khóa khi đã quyết toán cọc.
        ContractEvidenceKinds.MoveOut => contract.Status is ContractStatuses.Active or ContractStatuses.Terminated && !window.Settled,
        _ => contract.Status is ContractStatuses.Active or ContractStatuses.Terminated
    };

    private static string ClosedMessage(string kind, Window window) => kind switch
    {
        ContractEvidenceKinds.MoveIn =>
            $"Đã hết hạn upload hoặc xóa ảnh nhận phòng (hạn đến hết ngày {window.MoveInDeadline.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}).",
        ContractEvidenceKinds.MoveOut when window.Settled => "Đã quyết toán cọc nên không thêm hoặc xóa ảnh trả phòng được.",
        _ => "Hợp đồng này không còn nhận minh chứng."
    };

    private async Task<ContractEvidenceDto> BuildAsync(HandoverContractReadModel contract, int userId, string role,
        CancellationToken cancellationToken)
    {
        var deadline = await MoveInDeadlineAsync(contract, cancellationToken);
        var canUpload = role is RoleNames.Customer or RoleNames.Manager;
        var files = new List<ContractEvidenceFileDto>();
        foreach (var file in await repository.GetFilesAsync(contract.ContractId, cancellationToken))
        {
            var hidden = file.HiddenAtUtc is not null;
            files.Add(new ContractEvidenceFileDto(
                file.ContractEvidenceFileId, file.Kind, file.FileName, file.ContentType, file.SizeBytes,
                file.UploadedByName, file.UploadedByRole, UtcTimestamp.ForResponse(file.UploadedAtUtc),
                canUpload && file.UploadedById == userId && IsOpen(file.Kind, contract, deadline),
                hidden && role != RoleNames.Admin ? null : await storage.GetReadUrlAsync(file.ObjectPath),
                hidden, file.HiddenReason));
        }
        return new ContractEvidenceDto(contract.ContractId, deadline.MoveInDeadline,
            canUpload && IsOpen(ContractEvidenceKinds.MoveIn, contract, deadline),
            canUpload && IsOpen(ContractEvidenceKinds.MoveOut, contract, deadline),
            canUpload && IsOpen(ContractEvidenceKinds.SignedContract, contract, deadline),
            files);
    }

    // Kiểm tra dữ liệu THẬT của file (byte đầu/cuối), không tin đuôi file hay Content-Type trình duyệt gửi.
    private static void ValidateContent(ContractEvidenceUpload upload)
    {
        var content = upload.Content ?? [];
        var span = content.AsSpan();
        var max = upload.ContentType == "application/pdf" ? PdfMaxBytes : ImageMaxBytes;
        if (content.Length == 0 || content.Length > max)
        {
            throw new ContractBusinessRuleException("Ảnh tối đa 5 MB, PDF tối đa 10 MB.");
        }
        var valid = upload.ContentType switch
        {
            "image/jpeg" => content.Length >= 4 && span.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) &&
                            span[^2..].SequenceEqual(new byte[] { 0xFF, 0xD9 }),
            "image/png" => content.Length >= 20 &&
                           span.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) &&
                           span[^8..].SequenceEqual(new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 }),
            "image/webp" => content.Length >= 20 && span[..4].SequenceEqual("RIFF"u8) && span.Slice(8, 4).SequenceEqual("WEBP"u8) &&
                            BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4, 4)) == content.Length - 8,
            "application/pdf" => span.StartsWith("%PDF-"u8) && span[Math.Max(0, content.Length - 1024)..].IndexOf("%%EOF"u8) >= 0,
            _ => false
        };
        if (!valid)
        {
            throw new ContractBusinessRuleException("Chỉ nhận ảnh JPG, PNG, WEBP hoặc file PDF hợp lệ.");
        }
    }

    private static string CleanFileName(string? fileName)
    {
        var name = new string(Path.GetFileName(fileName ?? "").Where(character => !char.IsControl(character)).ToArray()).Trim();
        return name.Length == 0 ? "file" : name.Length > 255 ? name[..255] : name;
    }

    private async Task DeleteObjectQuietlyAsync(string objectPath)
    {
        try
        {
            await storage.DeleteAsync(objectPath);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not delete contract evidence object {ObjectPath}; remove it manually.", objectPath);
        }
    }
}
