using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IRentalService
{
    Task<IReadOnlyList<RentalDto>> GetCurrentRentalsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RentalDto>> GetRentalHistoryAsync(CancellationToken cancellationToken);
}

public sealed class RentalAuthenticationException : Exception
{
}
