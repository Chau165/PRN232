namespace PRN232.Application.Interfaces;

public interface IHealthCheckService
{
    Task<bool> IsDatabaseReachableAsync(CancellationToken cancellationToken);
}
