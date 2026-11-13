namespace PRN232.Application.Interfaces;

public interface IHealthCheckRepository
{
    // False when the database cannot be reached (refused connection, bad connection string, timeout).
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
