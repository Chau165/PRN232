using PRN232.Application.Interfaces;

namespace PRN232.Application.Services;

// Controller -> Service -> Repository: the health check controller no longer touches AppDbContext.
public sealed class HealthCheckService : IHealthCheckService
{
    private readonly IHealthCheckRepository _repository;

    public HealthCheckService(IHealthCheckRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> IsDatabaseReachableAsync(CancellationToken cancellationToken)
    {
        return _repository.CanConnectAsync(cancellationToken);
    }
}
