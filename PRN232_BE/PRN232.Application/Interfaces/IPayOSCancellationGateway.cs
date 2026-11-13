namespace PRN232.Application.Interfaces;

public interface IPayOSCancellationGateway
{
    Task CancelAsync(long orderCode, CancellationToken cancellationToken);
}
