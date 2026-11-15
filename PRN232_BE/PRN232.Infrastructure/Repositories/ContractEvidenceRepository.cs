using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class ContractEvidenceRepository(AppDbContext context) : IContractEvidenceRepository
{
    public Task<List<ContractEvidenceFileReadModel>> GetFilesAsync(int contractId, CancellationToken cancellationToken) =>
        context.ContractEvidenceFiles
            .AsNoTracking()
            .Where(file => file.ContractId == contractId)
            .OrderBy(file => file.ContractEvidenceFileId)
            .Select(file => new ContractEvidenceFileReadModel(file.ContractEvidenceFileId, file.Kind, file.ObjectPath,
                file.ContentType, file.SizeBytes, file.FileName, file.UploadedById, file.UploadedBy!.FullName,
                file.UploadedBy.Role!.RoleName, file.UploadedAtUtc, file.HiddenAtUtc, file.HiddenReason))
            .ToListAsync(cancellationToken);

    public Task<DateOnly?> GetMoveInHandoverDateAsync(int contractId, CancellationToken cancellationToken) =>
        context.HandoverRecords
            .AsNoTracking()
            .Where(record => record.ContractId == contractId && record.Kind == HandoverKinds.MoveIn)
            .Select(record => (DateOnly?)record.HandoverDate)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> AddAsync(ContractEvidenceFiles file, int maxFilesPerKind, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (context.Database.IsNpgsql())
        {
            // Hai lần upload song song không cùng vượt giới hạn số file.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = {file.ContractId} FOR UPDATE", cancellationToken);
        }
        var count = await context.ContractEvidenceFiles
            .CountAsync(item => item.ContractId == file.ContractId && item.Kind == file.Kind, cancellationToken);
        if (count >= maxFilesPerKind)
        {
            return false;
        }
        context.ContractEvidenceFiles.Add(file);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int fileId, CancellationToken cancellationToken) =>
        await context.ContractEvidenceFiles
            .Where(file => file.ContractEvidenceFileId == fileId)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<bool> HideAsync(int fileId, int adminId, string reason, DateTime hiddenAtUtc,
        CancellationToken cancellationToken) =>
        await context.ContractEvidenceFiles
            .Where(file => file.ContractEvidenceFileId == fileId && file.HiddenAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(file => file.HiddenAtUtc, hiddenAtUtc)
                .SetProperty(file => file.HiddenById, adminId)
                .SetProperty(file => file.HiddenReason, reason), cancellationToken) > 0;
}
