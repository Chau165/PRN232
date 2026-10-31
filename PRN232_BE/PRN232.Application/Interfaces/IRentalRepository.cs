using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

public interface IRentalRepository
{
    Task<IReadOnlyList<RentalReadModel>> GetCurrentRentalsAsync(
        int customerId,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RentalReadModel>> GetRentalHistoryAsync(
        int customerId,
        DateOnly today,
        CancellationToken cancellationToken);
}
