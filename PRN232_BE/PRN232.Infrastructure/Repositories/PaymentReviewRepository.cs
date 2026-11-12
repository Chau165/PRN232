using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class PaymentReviewRepository(AppDbContext context) : IPaymentReviewRepository
{
    private static IQueryable<PaymentReviewReadModel> Project(IQueryable<PaymentReviewCases> query) => query.Select(item =>
        new PaymentReviewReadModel(item.PaymentReviewCaseId, item.InvoiceId, item.Receipt!.PaymentRequestId!.Value,
            item.Reason, item.Status, item.Receipt.Amount, item.Receipt.Currency, item.Receipt.Reference,
            item.Receipt.Source, item.Receipt.TransactionDateTime, item.ExistingPaymentId, item.ExistingPayment!.PaymentMethod,
            item.ExistingPayment.Amount, item.ExistingPayment.PaidAtUtc, item.CreatedAtUtc, item.Resolution,
            item.ResolutionNote, item.EvidenceReference, item.ResolvedByAdminId, item.ResolvedAtUtc));

    public Task<List<PaymentReviewReadModel>> GetAllAsync(string status, int skip, int take, CancellationToken cancellationToken) =>
        Project(context.PaymentReviewCases.AsNoTracking().Where(item => item.Status == status)
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.PaymentReviewCaseId).Skip(skip).Take(take)).ToListAsync(cancellationToken);

    public Task<PaymentReviewReadModel?> GetAsync(int id, CancellationToken cancellationToken) =>
        Project(context.PaymentReviewCases.AsNoTracking().Where(item => item.PaymentReviewCaseId == id)).SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> ResolveAsync(int id, int adminId, string resolution, string note, string? evidence,
        DateTime nowUtc, CancellationToken cancellationToken) =>
        await context.PaymentReviewCases.Where(item => item.PaymentReviewCaseId == id && item.Status == "Open")
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Resolved")
                .SetProperty(item => item.Resolution, resolution).SetProperty(item => item.ResolutionNote, note)
                .SetProperty(item => item.EvidenceReference, evidence).SetProperty(item => item.ResolvedByAdminId, adminId)
                .SetProperty(item => item.ResolvedAtUtc, nowUtc), cancellationToken) == 1;
}
