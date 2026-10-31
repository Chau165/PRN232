using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class RentalRepository(AppDbContext context) : IRentalRepository
{
    public async Task<IReadOnlyList<RentalReadModel>> GetCurrentRentalsAsync(
        int customerId,
        DateOnly today,
        CancellationToken cancellationToken) =>
        await GetRentalsAsync(customerId, today, history: false, cancellationToken);

    public async Task<IReadOnlyList<RentalReadModel>> GetRentalHistoryAsync(
        int customerId,
        DateOnly today,
        CancellationToken cancellationToken) =>
        await GetRentalsAsync(customerId, today, history: true, cancellationToken);

    private async Task<IReadOnlyList<RentalReadModel>> GetRentalsAsync(
        int customerId,
        DateOnly today,
        bool history,
        CancellationToken cancellationToken)
    {
        var rentals = await context.Contracts
            .AsNoTracking()
            .Where(contract => contract.CustomerId == customerId &&
                contract.Status == ContractStatuses.Active &&
                (history
                    ? contract.StartDate <= today && contract.EndDate < today
                    : contract.StartDate <= today && contract.EndDate >= today))
            .OrderByDescending(contract => contract.StartDate)
            .ThenBy(contract => contract.ContractId)
            .Select(contract => new RentalBaseDto(
                contract.ContractId,
                contract.Status,
                contract.StartDate,
                contract.EndDate,
                contract.MonthlyRent,
                contract.DepositAmount,
                contract.Room!.RoomId,
                contract.Room.RoomCode,
                contract.Room.Status,
                contract.Room.AreaSqm,
                contract.Room.RoomType!.RoomTypeId,
                contract.Room.RoomType.TypeName,
                contract.Room.Property!.PropertyId,
                contract.Room.Property.PropertyName,
                contract.Room.Property.AddressText,
                contract.Room.Property.PrimaryImageUrl))
            .ToListAsync(cancellationToken);

        if (rentals.Count == 0)
        {
            return [];
        }

        var roomIds = rentals.Select(rental => rental.RoomId).Distinct().ToArray();
        var imageRows = await context.RoomImages
            .AsNoTracking()
            .Where(image => roomIds.Contains(image.RoomId))
            .OrderBy(image => image.RoomId)
            .ThenBy(image => image.RoomImageId)
            .Select(image => new RentalImageRow(image.RoomId, image.RoomImageId, image.ImageUrl, image.IsPrimary))
            .ToListAsync(cancellationToken);
        var equipmentRows = await context.Equipments
            .AsNoTracking()
            .Where(equipment => roomIds.Contains(equipment.RoomId))
            .OrderBy(equipment => equipment.RoomId)
            .ThenBy(equipment => equipment.EquipmentId)
            .Select(equipment => new RentalEquipmentRow(
                equipment.RoomId,
                new RentalEquipmentReadModel(equipment.EquipmentId, equipment.Name, equipment.Status)))
            .ToListAsync(cancellationToken);
        var amenityRows = await context.RoomAmenities
            .AsNoTracking()
            .Where(link => roomIds.Contains(link.RoomId))
            .OrderBy(link => link.RoomId)
            .ThenBy(link => link.AmenityId)
            .Select(link => new RentalAmenityRow(
                link.RoomId,
                new RentalAmenityReadModel(link.Amenity!.AmenityId, link.Amenity.Name)))
            .ToListAsync(cancellationToken);

        var imagesByRoom = imageRows.GroupBy(image => image.RoomId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var equipmentByRoom = equipmentRows.GroupBy(row => row.RoomId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RentalEquipmentReadModel>)group.Select(row => row.Equipment).ToArray());
        var amenitiesByRoom = amenityRows.GroupBy(row => row.RoomId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RentalAmenityReadModel>)group.Select(row => row.Amenity).ToArray());

        return rentals.Select(rental =>
        {
            imagesByRoom.TryGetValue(rental.RoomId, out var roomImages);
            var images = roomImages ?? [];
            var primaryImage = images
                .OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.RoomImageId)
                .Select(image => image.ImageUrl)
                .FirstOrDefault() ?? rental.PropertyPrimaryImageUrl;

            return new RentalReadModel(
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
                primaryImage,
                images.Select(image => image.ImageUrl).ToArray(),
                equipmentByRoom.GetValueOrDefault(rental.RoomId, []),
                amenitiesByRoom.GetValueOrDefault(rental.RoomId, []));
        }).ToArray();
    }

    private sealed record RentalBaseDto(
        int ContractId,
        string ContractStatus,
        DateOnly StartDate,
        DateOnly EndDate,
        decimal MonthlyRent,
        decimal DepositAmount,
        int RoomId,
        string RoomCode,
        string RoomStatus,
        decimal AreaSqm,
        int RoomTypeId,
        string RoomTypeName,
        int PropertyId,
        string PropertyName,
        string PropertyAddress,
        string? PropertyPrimaryImageUrl);

    private sealed record RentalImageRow(int RoomId, int RoomImageId, string ImageUrl, bool IsPrimary);
    private sealed record RentalEquipmentRow(int RoomId, RentalEquipmentReadModel Equipment);
    private sealed record RentalAmenityRow(int RoomId, RentalAmenityReadModel Amenity);
}
