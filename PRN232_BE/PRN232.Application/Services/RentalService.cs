using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;

namespace PRN232.Application.Services;

public sealed class RentalService : IRentalService
{
    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private readonly IRentalRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public RentalService(
        IRentalRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<RentalDto>> GetCurrentRentalsAsync(CancellationToken cancellationToken)
    {
        var customerId = GetCustomerId();
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        var rentals = await _repository.GetCurrentRentalsAsync(customerId, today, cancellationToken);
        return rentals.Select(ConvertToDto).ToArray();
    }

    public async Task<IReadOnlyList<RentalDto>> GetRentalHistoryAsync(CancellationToken cancellationToken)
    {
        var customerId = GetCustomerId();
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        var rentals = await _repository.GetRentalHistoryAsync(customerId, today, cancellationToken);
        return rentals.Select(ConvertToDto).ToArray();
    }

    private static RentalDto ConvertToDto(RentalReadModel rental)
    {
        return new RentalDto(
            rental.ContractId,
            rental.ContractStatus,
            rental.StartDate,
            rental.EndDate,
            rental.MonthlyRent,
            rental.DepositAmount,
            rental.RoomId,
            rental.RoomCode,
            rental.RoomStatus,
            rental.AreaSqm,
            rental.RoomTypeId,
            rental.RoomTypeName,
            rental.PropertyId,
            rental.PropertyName,
            rental.PropertyAddress,
            rental.PrimaryImageUrl,
            rental.Images,
            rental.Equipments.Select(ConvertToDto).ToArray(),
            rental.Amenities.Select(ConvertToDto).ToArray());
    }

    private static RentalEquipmentDto ConvertToDto(RentalEquipmentReadModel equipment)
    {
        return new RentalEquipmentDto(
            equipment.EquipmentId,
            equipment.Name,
            equipment.Status);
    }

    private static RentalAmenityDto ConvertToDto(RentalAmenityReadModel amenity)
    {
        return new RentalAmenityDto(
            amenity.AmenityId,
            amenity.Name);
    }

    private int GetCustomerId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not > 0 ||
            _currentUser.Role != RoleNames.Customer)
        {
            throw new RentalAuthenticationException();
        }

        return _currentUser.UserId.Value;
    }
}
