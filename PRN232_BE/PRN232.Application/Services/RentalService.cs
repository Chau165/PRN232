using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;

namespace PRN232.Application.Services;

public sealed class RentalService(
    IRentalRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IRentalService
{
    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<IReadOnlyList<RentalDto>> GetCurrentRentalsAsync(CancellationToken cancellationToken)
    {
        var customerId = GetCustomerId();
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        var rentals = await repository.GetCurrentRentalsAsync(customerId, today, cancellationToken);
        return rentals.Select(ConvertToDto).ToArray();
    }

    public async Task<IReadOnlyList<RentalDto>> GetRentalHistoryAsync(CancellationToken cancellationToken)
    {
        var customerId = GetCustomerId();
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        var rentals = await repository.GetRentalHistoryAsync(customerId, today, cancellationToken);
        return rentals.Select(ConvertToDto).ToArray();
    }

    private static RentalDto ConvertToDto(RentalReadModel rental) => new(
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

    private static RentalEquipmentDto ConvertToDto(RentalEquipmentReadModel equipment) => new(
        equipment.EquipmentId,
        equipment.Name,
        equipment.Status);

    private static RentalAmenityDto ConvertToDto(RentalAmenityReadModel amenity) => new(
        amenity.AmenityId,
        amenity.Name);

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
