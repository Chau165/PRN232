using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.States;

namespace PRN232.Application.Services;

public sealed class RentalService(
    IRentalRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IRentalService
{
    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public Task<IReadOnlyList<RentalDto>> GetCurrentRentalsAsync(CancellationToken cancellationToken)
    {
        var customerId = GetCustomerId();
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        return repository.GetCurrentRentalsAsync(customerId, today, cancellationToken);
    }

    public Task<IReadOnlyList<RentalDto>> GetRentalHistoryAsync(CancellationToken cancellationToken)
    {
        var customerId = GetCustomerId();
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        return repository.GetRentalHistoryAsync(customerId, today, cancellationToken);
    }

    private int GetCustomerId()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not > 0 ||
            currentUser.Role != RoleNames.Customer)
        {
            throw new RentalAuthenticationException();
        }

        return currentUser.UserId.Value;
    }
}
