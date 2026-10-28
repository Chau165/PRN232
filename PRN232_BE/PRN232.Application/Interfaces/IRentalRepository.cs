using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IRentalRepository
{
    Task<IReadOnlyList<RentalDto>> GetCurrentRentalsAsync(
        int customerId,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RentalDto>> GetRentalHistoryAsync(
        int customerId,
        DateOnly today,
        CancellationToken cancellationToken);
}
