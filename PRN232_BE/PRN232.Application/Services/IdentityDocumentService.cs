using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using DomainIdentityDocument = PRN232.Domain.Entities.IdentityDocuments;

namespace PRN232.Application.Services;

public class IdentityDocumentService : IIdentityDocumentService
{
    private readonly IIdentityDocumentRepository _repository;
    private readonly ICccdHasher _cccdHasher;

    public IdentityDocumentService(IIdentityDocumentRepository repository, ICccdHasher cccdHasher)
    {
        _repository = repository;
        _cccdHasher = cccdHasher;
    }

    // Admin sees everything; a Manager only sees customers with a booking or contract on a property
    // assigned to them (A3).
    public async Task<List<IdentityDocumentDto>> GetAllAsync(string? status, int userId, string role)
    {
        int? managerId = role switch
        {
            RoleNames.Admin => null,
            RoleNames.Manager => userId,
            _ => throw new IdentityDocumentAuthorizationException("Bạn không có quyền xem danh sách hồ sơ.")
        };
        var documents = await _repository.GetAllAsync(status, managerId);
        return documents.Select(document => ConvertToDto(document, revealCccd: false)).ToList();
    }

    public async Task<List<IdentityDocumentDto>> GetMineAsync(int customerId)
    {
        var documents = await _repository.GetByCustomerIdAsync(customerId);
        return documents.Select(document => ConvertToDto(document, revealCccd: false)).ToList();
    }

    public async Task<IdentityDocumentDto?> GetByIdAsync(int id, int userId, string role)
    {
        var document = await _repository.GetByIdAsync(id);
        if (document is null)
        {
            return null;
        }
        await EnsureCanAccessAsync(document.CustomerId, userId, role);
        // A9: số đầy đủ chỉ trả cho Admin/Manager (đã qua kiểm tra quyền với khách này) đang mở
        // chi tiết hồ sơ để duyệt; mọi chỗ khác chỉ thấy 4 số cuối.
        var isReviewer = role is RoleNames.Admin or RoleNames.Manager;
        return ConvertToDto(document, revealCccd: isReviewer);
    }

    public async Task<IdentityDocumentDto> SubmitAsync(SubmitIdentityDocumentDto dto, int customerId)
    {
        if (string.IsNullOrWhiteSpace(dto.CCCDNumber))
        {
            throw new IdentityDocumentValidationException("Số CCCD không được để trống.");
        }

        var cccdNumber = dto.CCCDNumber.Trim();
        if (await _repository.HasPendingOrApprovedAsync(customerId))
        {
            throw new IdentityDocumentPendingExistsException();
        }
        var cccdNumberHash = _cccdHasher.Hash(cccdNumber);
        if (await _repository.CccdNumberUsedByOtherAsync(cccdNumberHash, customerId))
        {
            throw new IdentityDocumentDuplicateException(cccdNumber);
        }

        var status = IdentityDocumentStatuses.Pending;
        var document = CreateIdentityDocumentEntity(customerId, cccdNumber, cccdNumberHash, status);

        var created = await _repository.AddAsync(document);
        await _repository.SaveChangesAsync();

        return ConvertToDto(await GetDocumentResponseAsync(created.IdentityDocumentId), revealCccd: false);
    }

    public async Task<IdentityDocumentDto?> ReviewAsync(int id, ReviewIdentityDocumentDto dto, int reviewerId, string role)
    {
        if (role is not (RoleNames.Admin or RoleNames.Manager))
        {
            throw new IdentityDocumentAuthorizationException("Chỉ quản lý hoặc admin mới được duyệt hồ sơ.");
        }

        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        await EnsureCanAccessAsync(entity.CustomerId, reviewerId, role);
        if (!IdentityDocumentStatuses.IsAllowedTransition(entity.Status, dto.Status))
        {
            throw new IdentityDocumentStatusTransitionException(
                $"Không thể chuyển trạng thái từ '{entity.Status}' sang '{dto.Status}'.");
        }

        entity.Status = dto.Status;
        entity.ReviewedBy = reviewerId;

        await _repository.SaveChangesAsync();
        var updated = await _repository.GetByIdAsync(id);
        if (updated is null)
        {
            return null;
        }
        return ConvertToDto(updated, revealCccd: false);
    }

    private async Task EnsureCanAccessAsync(int customerId, int userId, string role)
    {
        var allowed = role switch
        {
            RoleNames.Admin => true,
            RoleNames.Manager => await _repository.IsCustomerOfManagerAsync(customerId, userId),
            RoleNames.Customer => customerId == userId,
            _ => false
        };
        if (!allowed)
        {
            throw new IdentityDocumentAuthorizationException("Bạn không có quyền với hồ sơ này.");
        }
    }

    private static DomainIdentityDocument CreateIdentityDocumentEntity(
        int customerId,
        string cccdNumber,
        string cccdNumberHash,
        string status)
    {
        return new DomainIdentityDocument
        {
            CustomerId = customerId,
            CCCDNumber = cccdNumber,
            CCCDNumberHash = cccdNumberHash,
            Status = status
        };
    }

    private static IdentityDocumentDto ConvertToDto(IdentityDocumentReadModel readModel, bool revealCccd)
    {
        return new IdentityDocumentDto(
            readModel.IdentityDocumentId,
            readModel.CustomerId,
            readModel.CustomerName,
            revealCccd ? readModel.CCCDNumber : CccdMasking.MaskNumber(readModel.CCCDNumber),
            readModel.Status,
            readModel.ReviewedBy,
            readModel.ReviewedByName);
    }

    private async Task<IdentityDocumentReadModel> GetDocumentResponseAsync(int documentId)
    {
        var document = await _repository.GetByIdAsync(documentId);
        if (document is null)
        {
            throw new InvalidOperationException("Created identity document was not found.");
        }
        return document;
    }
}

public class IdentityDocumentBusinessRuleException(string message) : Exception(message);

public sealed class IdentityDocumentValidationException(string message)
    : IdentityDocumentBusinessRuleException(message);

// Thông báo chỉ chứa 4 số cuối để số CCCD đầy đủ không lọt vào log lỗi (A9).
public sealed class IdentityDocumentDuplicateException(string cccdNumber)
    : IdentityDocumentBusinessRuleException(
        $"Số CCCD '{CccdMasking.MaskNumber(cccdNumber)}' đã được dùng bởi tài khoản khác.");

public sealed class IdentityDocumentPendingExistsException()
    : IdentityDocumentBusinessRuleException(
        "Bạn đã có hồ sơ đang chờ duyệt hoặc đã được duyệt — không thể nộp thêm.");

public sealed class IdentityDocumentStatusTransitionException(string message)
    : IdentityDocumentBusinessRuleException(message);

public sealed class IdentityDocumentAuthorizationException(string message) : Exception(message);
