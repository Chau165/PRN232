using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;

namespace PRN232.Application.Services;

public class PaymentReviewService(IPaymentReviewRepository repository, TimeProvider clock) : IPaymentReviewService
{
    private static void RequireAdmin(int userId, string role)
    {
        if (userId <= 0 || role != RoleNames.Admin) throw new InvoiceAuthorizationException("Only Admin can review duplicate payments.");
    }

    public async Task<List<PaymentReviewDto>> GetAllAsync(int userId, string role, string status, int page, int pageSize, CancellationToken cancellationToken)
    {
        RequireAdmin(userId, role);
        if (status is not ("Open" or "Resolved") || page is < 1 or > 100000 || pageSize is < 1 or > 100)
            throw new InvoiceBusinessRuleException("Status must be Open/Resolved, page 1..100000, pageSize 1..100.");
        return (await repository.GetAllAsync(status, (page - 1) * pageSize, pageSize, cancellationToken)).Select(ConvertToDto).ToList();
    }

    public async Task<PaymentReviewDto?> GetAsync(int id, int userId, string role, CancellationToken cancellationToken)
    {
        RequireAdmin(userId, role);
        if (id <= 0) throw new InvoiceBusinessRuleException("Review ID must be positive.");
        var item = await repository.GetAsync(id, cancellationToken);
        return item is null ? null : ConvertToDto(item);
    }

    public async Task<PaymentReviewDto> ResolveAsync(int id, int userId, string role, ResolvePaymentReviewDto dto, CancellationToken cancellationToken)
    {
        RequireAdmin(userId, role);
        if (id <= 0 || dto.Resolution is not ("RefundedExternally" or "NotDuplicate") ||
            string.IsNullOrWhiteSpace(dto.Note) || dto.Note.Trim().Length is < 5 or > 2000 ||
            dto.EvidenceReference?.Length > 256 || (dto.Resolution == "RefundedExternally" && string.IsNullOrWhiteSpace(dto.EvidenceReference)))
            throw new InvoiceBusinessRuleException("A valid resolution and note (5..2000 characters) are required; external refunds require an evidence reference (max 256).");
        if (await repository.GetAsync(id, cancellationToken) is null) throw new InvoiceNotFoundException("Payment review does not exist.");
        if (!await repository.ResolveAsync(id, userId, dto.Resolution, dto.Note.Trim(), dto.EvidenceReference?.Trim(),
                clock.GetUtcNow().UtcDateTime, cancellationToken))
            throw new InvoiceConflictException("This review was already resolved or changed. Refresh before continuing.");
        return ConvertToDto((await repository.GetAsync(id, cancellationToken))!);
    }

    private static PaymentReviewDto ConvertToDto(PaymentReviewReadModel item) => new(item.Id, item.InvoiceId, item.PaymentRequestId,
        item.Reason, item.Status, item.AdditionalAmount, item.Currency, item.ProviderReference, item.Source, item.TransactionDateTime,
        item.ExistingPaymentId, item.ExistingPaymentMethod, item.ExistingAmount, item.ExistingPaidAtUtc,
        item.CreatedAtUtc, item.Resolution, item.ResolutionNote, item.EvidenceReference, item.ResolvedByAdminId, item.ResolvedAtUtc);
}
