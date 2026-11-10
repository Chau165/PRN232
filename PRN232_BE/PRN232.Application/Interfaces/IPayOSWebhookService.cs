namespace PRN232.Application.Interfaces;

public interface IPayOSWebhookService
{
    Task ReceiveAsync(string json, CancellationToken cancellationToken);
}
