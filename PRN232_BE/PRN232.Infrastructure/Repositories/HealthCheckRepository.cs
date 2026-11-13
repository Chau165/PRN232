using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class HealthCheckRepository : IHealthCheckRepository
{
    private readonly AppDbContext _context;

    public HealthCheckRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Some providers throw instead of returning false (bad connection string, timeout).
            return false;
        }
    }
}
