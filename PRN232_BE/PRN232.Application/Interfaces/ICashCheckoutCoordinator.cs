namespace PRN232.Application.Interfaces;

public interface ICashCheckoutCoordinator
{
    Task PrepareAsync(int invoiceId, int? ownerId, CancellationToken cancellationToken);
}
