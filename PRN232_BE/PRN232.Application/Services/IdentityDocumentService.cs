using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using DomainIdentityDocument = PRN232.Domain.Entities.IdentityDocuments;

namespace PRN232.Application.Services;

public class IdentityDocumentService : IIdentityDocumentService
{
    private readonly IIdentityDocumentRepository _repository;

    public IdentityDocumentService(IIdentityDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<IdentityDocumentDto>> GetAllAsync(string? status)
    {
        var documents = await _repository.GetAllAsync(status);
        return documents.Select(ConvertToDto).ToList();
    }

    public async Task<List<IdentityDocumentDto>> GetMineAsync(int customerId)
    {
        var documents = await _repository.GetByCustomerIdAsync(customerId);
        return documents.Select(ConvertToDto).ToList();
    }

    public async Task<IdentityDocumentDto?> GetByIdAsync(int id)
    {
        var document = await _repository.GetByIdAsync(id);
        if (document is null)
        {
            return null;
        }
        return ConvertToDto(document);
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
        if (await _repository.CccdNumberUsedByOtherAsync(cccdNumber, customerId))
        {
            throw new IdentityDocumentDuplicateException(cccdNumber);
        }

        var status = IdentityDocumentStatuses.Pending;
        var document = CreateIdentityDocumentEntity(customerId, cccdNumber, status);

        var created = await _repository.AddAsync(document);
        await _repository.SaveChangesAsync();

        return ConvertToDto(await GetDocumentResponseAsync(created.IdentityDocumentId));
    }

    public async Task<IdentityDocumentDto?> ReviewAsync(int id, ReviewIdentityDocumentDto dto, int reviewerId)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
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
        var updated = await _repository.GetByIdAsync(id);
        if (updated is null)
        {
            return null;
        }
        return ConvertToDto(updated);
    }

    private static DomainIdentityDocument CreateIdentityDocumentEntity(int customerId, string cccdNumber, string status)
    {
        return new DomainIdentityDocument
        {
            CustomerId = customerId,
            CCCDNumber = cccdNumber,
            Status = status
        };
    }

    private static IdentityDocumentDto ConvertToDto(IdentityDocumentReadModel readModel)
    {
        return new IdentityDocumentDto(
            readModel.IdentityDocumentId,
            readModel.CustomerId,
            readModel.CustomerName,
            readModel.CCCDNumber,
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

public sealed class IdentityDocumentDuplicateException(string cccdNumber)
    : IdentityDocumentBusinessRuleException($"Số CCCD '{cccdNumber}' đã được dùng bởi tài khoản khác.");

public sealed class IdentityDocumentPendingExistsException()
    : IdentityDocumentBusinessRuleException(
        "Bạn đã có hồ sơ đang chờ duyệt hoặc đã được duyệt — không thể nộp thêm.");

public sealed class IdentityDocumentStatusTransitionException(string message)
    : IdentityDocumentBusinessRuleException(message);
