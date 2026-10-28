using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class IdentityDocumentService : IIdentityDocumentService
{
    private readonly IIdentityDocumentRepository _repository;

    public IdentityDocumentService(IIdentityDocumentRepository repository)
    {
        _repository = repository;
    }

    public Task<List<IdentityDocumentDto>> GetAllAsync(string? status) => _repository.GetAllAsync(status);

    public Task<List<IdentityDocumentDto>> GetMineAsync(int customerId) =>
        _repository.GetByCustomerIdAsync(customerId);

    public Task<IdentityDocumentDto?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

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
        if (await _repository.CccdNumberUsedByOtherAsync(cccdNumber, customerId))
        {
            throw new IdentityDocumentDuplicateException(cccdNumber);
        }

        var document = new IdentityDocuments
        {
            CustomerId = customerId,
            CCCDNumber = cccdNumber,
            Status = IdentityDocumentStatuses.Pending,
        };

        var created = await _repository.AddAsync(document);
        await _repository.SaveChangesAsync();

        return (await _repository.GetByIdAsync(created.IdentityDocumentId))!;
    }

    public async Task<IdentityDocumentDto?> ReviewAsync(int id, ReviewIdentityDocumentDto dto, int reviewerId)
    {
        var entity = await _repository.GetEntityByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        if (!IdentityDocumentStatuses.IsAllowedTransition(entity.Status, dto.Status))
        {
            throw new IdentityDocumentStatusTransitionException(
                $"Không thể chuyển trạng thái từ '{entity.Status}' sang '{dto.Status}'.");
        }

        entity.Status = dto.Status;
        entity.ReviewedBy = reviewerId;

        await _repository.SaveChangesAsync();
        return await _repository.GetByIdAsync(id);
    }
}

public class IdentityDocumentBusinessRuleException(string message) : Exception(message);

public sealed class IdentityDocumentValidationException(string message)
    : IdentityDocumentBusinessRuleException(message);

public sealed class IdentityDocumentDuplicateException(string cccdNumber)
    : IdentityDocumentBusinessRuleException($"Số CCCD '{cccdNumber}' đã được dùng bởi tài khoản khác.");

public sealed class IdentityDocumentPendingExistsException()
    : IdentityDocumentBusinessRuleException(
        "Bạn đã có hồ sơ đang chờ duyệt hoặc đã được duyệt — không thể nộp thêm.");

public sealed class IdentityDocumentStatusTransitionException(string message)
    : IdentityDocumentBusinessRuleException(message);
