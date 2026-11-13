using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Security;

/// <summary>
/// One-off data migration for A9. Rows written before encryption have a plaintext CCCDNumber and
/// no CCCDNumberHash. Re-saving them through the value converter encrypts the number, and the hash
/// is filled in for the duplicate check. Safe to run on every startup: it only touches rows whose
/// hash is still missing. (A SQL migration cannot do this because it has no access to the Data
/// Protection keys.)
/// </summary>
public sealed class IdentityDocumentEncryptionBackfill
{
    private const int BatchSize = 200;
    private readonly AppDbContext _context;
    private readonly ICccdHasher _hasher;

    public IdentityDocumentEncryptionBackfill(AppDbContext context, ICccdHasher hasher)
    {
        _context = context;
        _hasher = hasher;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var updated = 0;
        while (true)
        {
            var batch = await _context.IdentityDocuments
                .Where(document => document.CCCDNumberHash == null)
                .OrderBy(document => document.IdentityDocumentId)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                return updated;
            }

            foreach (var document in batch)
            {
                document.CCCDNumberHash = _hasher.Hash(document.CCCDNumber);
                // Force a write so the converter stores the encrypted form of the same value.
                _context.Entry(document).Property(entity => entity.CCCDNumber).IsModified = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            updated += batch.Count;
        }
    }
}
