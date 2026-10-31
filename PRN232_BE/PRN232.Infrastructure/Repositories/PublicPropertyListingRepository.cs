using System.Data;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Normalizers;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class PublicPropertyListingRepository : IPublicPropertyListingRepository
{
    private const int MaximumAmenitiesSummary = 5;

    private readonly AppDbContext _context;

    public PublicPropertyListingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> AmenityIdsExistAsync(int[] amenityIds, CancellationToken cancellationToken) =>
        await _context.Amenities
            .AsNoTracking()
            .Where(amenity => amenityIds.Contains(amenity.AmenityId))
            .Select(amenity => amenity.AmenityId)
            .Distinct()
            .CountAsync(cancellationToken) == amenityIds.Length;

    public async Task<PublicPropertyPageReadModel> GetPageAsync(
        PublicPropertySearchCriteria criteria,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;

        var matchingRooms = ApplyRoomFilters(
            RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc),
            criteria);
        var eligibleProperties = ApplyPropertyFilters(_context.Properties.AsNoTracking(), criteria)
            .Where(property => matchingRooms.Any(room => room.PropertyId == property.PropertyId));

        var totalCount = await eligibleProperties.CountAsync(cancellationToken);
        var listingRows = eligibleProperties.Select(property => new
        {
            property.PropertyId,
            property.PropertyName,
            property.PrimaryImageUrl,
            property.AddressText,
            property.Ward,
            property.District,
            property.City,
            AvailableRoomCount = matchingRooms
                .Where(room => room.PropertyId == property.PropertyId)
                .Select(room => room.RoomId)
                .Distinct()
                .Count(),
            StartingPrice = matchingRooms
                .Where(room => room.PropertyId == property.PropertyId)
                .Select(room => (decimal?)room.RoomType!.Price)
                .Min(),
            RepresentativeRoomId = matchingRooms
                .Where(room => room.PropertyId == property.PropertyId)
                .OrderBy(room => room.RoomType!.Price)
                .ThenBy(room => room.RoomId)
                .Select(room => (int?)room.RoomId)
                .FirstOrDefault()
        });

        var orderedRows = criteria.SortBy switch
        {
            "price_asc" => listingRows
                .OrderBy(row => row.StartingPrice)
                .ThenBy(row => row.PropertyId),
            "price_desc" => listingRows
                .OrderByDescending(row => row.StartingPrice)
                .ThenBy(row => row.PropertyId),
            _ => listingRows.OrderBy(row => row.PropertyId)
        };

        var pageRows = await orderedRows
            .Skip(criteria.Offset)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);
        var representativeRoomIds = pageRows
            .Where(row => row.RepresentativeRoomId.HasValue)
            .Select(row => row.RepresentativeRoomId!.Value)
            .ToArray();

        var amenityRows = await _context.RoomAmenities
            .AsNoTracking()
            .Where(link => representativeRoomIds.Contains(link.RoomId))
            .OrderBy(link => link.RoomId)
            .ThenBy(link => link.AmenityId)
            .Select(link => new
            {
                link.RoomId,
                AmenityId = link.Amenity!.AmenityId,
                link.Amenity.Name
            })
            .ToListAsync(cancellationToken);

        var amenitiesByRoom = amenityRows
            .GroupBy(link => link.RoomId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PublicAmenitySummaryReadModel>)group
                    .Take(MaximumAmenitiesSummary)
                    .Select(link => new PublicAmenitySummaryReadModel(link.AmenityId, link.Name))
                    .ToArray());

        var items = pageRows
            .Select(row => new PublicPropertyCardReadModel(
                row.PropertyId,
                row.PropertyName,
                row.AddressText,
                row.Ward,
                row.District,
                row.City,
                row.PrimaryImageUrl,
                row.RepresentativeRoomId is int roomId && amenitiesByRoom.TryGetValue(roomId, out var amenities)
                    ? amenities
                     : Array.Empty<PublicAmenitySummaryReadModel>(),
                row.AvailableRoomCount,
                row.StartingPrice))
            .ToArray();

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new PublicPropertyPageReadModel(items, totalCount, criteria.Page, criteria.PageSize);
    }

    public async Task<IReadOnlyList<PublicPropertyAreaCardReadModel>> GetByAreaAsync(
        PublicPropertyAreaSearchCriteria criteria,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;

        var rentableRooms = RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc);
        var propertyRows = await _context.Properties
            .AsNoTracking()
            .Where(property => rentableRooms.Any(room => room.PropertyId == property.PropertyId))
            .OrderBy(property => property.PropertyId)
            .Select(property => new
            {
                property.PropertyId,
                property.PropertyName,
                property.AddressText,
                property.Ward,
                property.District,
                property.City,
                property.Latitude,
                property.Longitude,
                property.PrimaryImageUrl,
                AvailableRoomCount = rentableRooms
                    .Where(room => room.PropertyId == property.PropertyId)
                    .Select(room => room.RoomId)
                    .Distinct()
                    .Count(),
                StartingPrice = rentableRooms
                    .Where(room => room.PropertyId == property.PropertyId)
                    .Select(room => (decimal?)room.RoomType!.Price)
                    .Min()
            })
            .ToListAsync(cancellationToken);

        // Normalize the already allowlisted rentable-property projection without relying on a database-specific unaccent extension.
        var items = propertyRows
            .Where(property =>
                (criteria.City is null || PublicAreaNameNormalizer.Normalize(property.City) == criteria.City) &&
                (criteria.District is null || PublicAreaNameNormalizer.Normalize(property.District) == criteria.District) &&
                (criteria.Ward is null || PublicAreaNameNormalizer.Normalize(property.Ward) == criteria.Ward))
            .Select(property => new PublicPropertyAreaCardReadModel(
                property.PropertyId,
                property.PropertyName,
                property.AddressText,
                property.Ward,
                property.District,
                property.City,
                property.Latitude,
                property.Longitude,
                property.PrimaryImageUrl,
                property.AvailableRoomCount,
                property.StartingPrice))
            .ToArray();

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return items;
    }

    public Task<PublicPropertyDetailReadModel?> GetDetailAsync(
        int propertyId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken) =>
        GetDetailCoreAsync(propertyId, asOfUtc, false, null, cancellationToken);

    public Task<PublicPropertyDetailReadModel?> GetEnrichedDetailAsync(
        int propertyId,
        DateTimeOffset asOfUtc,
        DateOnly currentMonthStart,
        CancellationToken cancellationToken) =>
        GetDetailCoreAsync(propertyId, asOfUtc, true, currentMonthStart, cancellationToken);

    private async Task<PublicPropertyDetailReadModel?> GetDetailCoreAsync(
        int propertyId,
        DateTimeOffset asOfUtc,
        bool includeEnrichment,
        DateOnly? currentMonthStart,
        CancellationToken cancellationToken)
    {
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;

        var rentableRooms = RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc);
        var detail = await _context.Properties
            .AsNoTracking()
            .Where(property => property.PropertyId == propertyId &&
                rentableRooms.Any(room => room.PropertyId == property.PropertyId))
            .Select(property => new
            {
                property.PropertyId,
                property.PropertyName,
                property.AddressText,
                property.Ward,
                property.District,
                property.City,
                property.PrimaryImageUrl,
                AvailableRoomCount = rentableRooms
                    .Where(room => room.PropertyId == property.PropertyId)
                    .Select(room => room.RoomId)
                    .Distinct()
                    .Count(),
                StartingPrice = rentableRooms
                    .Where(room => room.PropertyId == property.PropertyId)
                    .Select(room => (decimal?)room.RoomType!.Price)
                    .Min()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (detail is null)
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return null;
        }

        var roomAmenities = await (
            from roomAmenity in _context.RoomAmenities.AsNoTracking()
            join room in rentableRooms on roomAmenity.RoomId equals room.RoomId
            join amenity in _context.Amenities.AsNoTracking() on roomAmenity.AmenityId equals amenity.AmenityId
            where room.PropertyId == propertyId
            group roomAmenity.RoomId by new
            {
                amenity.AmenityId,
                amenity.Name
            } into amenityRooms
            orderby amenityRooms.Key.AmenityId
            select new PublicPropertyRoomAmenityReadModel(
                amenityRooms.Key.AmenityId,
                amenityRooms.Key.Name,
                amenityRooms.Count()))
            .ToListAsync(cancellationToken);

        string? description = null;
        IReadOnlyList<PublicAmenitySummaryReadModel> commonAmenities = Array.Empty<PublicAmenitySummaryReadModel>();
        IReadOnlyList<PublicPropertyServiceRateReadModel> livingCosts = Array.Empty<PublicPropertyServiceRateReadModel>();
        if (includeEnrichment)
        {
            description = await _context.Properties
                .AsNoTracking()
                .Where(property => property.PropertyId == propertyId)
                .Select(property => property.Description)
                .FirstOrDefaultAsync(cancellationToken);

            commonAmenities = await _context.PropertyAmenities
                .AsNoTracking()
                .Where(link => link.PropertyId == propertyId)
                .OrderBy(link => link.Amenity!.Name)
                .ThenBy(link => link.AmenityId)
                .Select(link => new PublicAmenitySummaryReadModel(link.AmenityId, link.Amenity!.Name))
                .ToListAsync(cancellationToken);

            var effectiveDate = currentMonthStart
                ?? throw new InvalidOperationException("An effective date is required when property detail enrichment is enabled.");
            livingCosts = await _context.PropertyServiceRates
                .AsNoTracking()
                .Where(rate => rate.PropertyId == propertyId &&
                    rate.EffectiveFrom <= effectiveDate &&
                    (rate.EffectiveTo == null || rate.EffectiveTo > effectiveDate))
                .OrderBy(rate => rate.Service!.Name)
                .ThenBy(rate => rate.ServiceId)
                .Select(rate => new PublicPropertyServiceRateReadModel(
                    rate.ServiceId,
                    rate.Service!.Name,
                    rate.Price,
                    rate.UnitCode,
                    rate.BillingBasis,
                    rate.EffectiveFrom,
                    rate.EffectiveTo))
                .ToListAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new PublicPropertyDetailReadModel(
            detail.PropertyId,
            detail.PropertyName,
            detail.AddressText,
            detail.Ward,
            detail.District,
            detail.City,
            detail.PrimaryImageUrl,
            detail.AvailableRoomCount,
            detail.StartingPrice,
            roomAmenities)
        {
            Description = description,
            CommonAmenities = commonAmenities,
            LivingCosts = livingCosts
        };
    }

    public async Task<PublicPropertyRoomPageReadModel?> GetRoomsAsync(
        int propertyId,
        PublicPropertySearchCriteria criteria,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;

        var rentableRooms = RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc);
        var hasRentableRooms = await _context.Properties
            .AsNoTracking()
            .AnyAsync(
                property => property.PropertyId == propertyId &&
                    rentableRooms.Any(room => room.PropertyId == property.PropertyId),
                cancellationToken);
        if (!hasRentableRooms)
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return null;
        }

        var matchingRooms = ApplyRoomFilters(
            rentableRooms.Where(room => room.PropertyId == propertyId),
            criteria);
        var totalCount = await matchingRooms
            .Select(room => room.RoomId)
            .Distinct()
            .CountAsync(cancellationToken);

        var orderedRooms = criteria.SortBy switch
        {
            "price_asc" => matchingRooms
                .OrderBy(room => room.RoomType!.Price)
                .ThenBy(room => room.RoomId),
            "price_desc" => matchingRooms
                .OrderByDescending(room => room.RoomType!.Price)
                .ThenBy(room => room.RoomId),
            _ => matchingRooms.OrderBy(room => room.RoomId)
        };
        var pageRows = await orderedRooms
            .Skip(criteria.Offset)
            .Take(criteria.PageSize)
            .Select(room => new
            {
                room.RoomId,
                room.RoomCode,
                room.RoomTypeId,
                RoomTypeName = room.RoomType!.TypeName,
                BaseRent = room.RoomType.Price,
                room.AreaSqm
            })
            .ToListAsync(cancellationToken);
        var roomIds = pageRows.Select(room => room.RoomId).ToArray();
        var amenityRows = await _context.RoomAmenities
            .AsNoTracking()
            .Where(link => roomIds.Contains(link.RoomId))
            .OrderBy(link => link.RoomId)
            .ThenBy(link => link.AmenityId)
            .Select(link => new
            {
                link.RoomId,
                AmenityId = link.Amenity!.AmenityId,
                link.Amenity.Name
            })
            .ToListAsync(cancellationToken);

        var amenitiesByRoom = amenityRows
            .GroupBy(link => link.RoomId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PublicAmenitySummaryReadModel>)group
                    .Take(MaximumAmenitiesSummary)
                    .Select(link => new PublicAmenitySummaryReadModel(link.AmenityId, link.Name))
                    .ToArray());
        var items = pageRows
            .Select(room => new PublicPropertyRoomCardReadModel(
                room.RoomId,
                room.RoomCode,
                room.RoomTypeId,
                room.RoomTypeName,
                room.BaseRent,
                room.AreaSqm,
                null,
                amenitiesByRoom.TryGetValue(room.RoomId, out var amenities)
                    ? amenities
                     : Array.Empty<PublicAmenitySummaryReadModel>()))
            .ToArray();

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new PublicPropertyRoomPageReadModel(items, totalCount, criteria.Page, criteria.PageSize);
    }

    public async Task<PublicRoomDetailReadModel?> GetRoomDetailAsync(
        int roomId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;

        var room = await RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc)
            .Where(candidate => candidate.RoomId == roomId)
            .Select(candidate => new
            {
                candidate.RoomId,
                candidate.RoomCode,
                candidate.AreaSqm,
                RoomTypeId = candidate.RoomType!.RoomTypeId,
                RoomType = new PublicRoomTypeReadModel(candidate.RoomType.RoomTypeId, candidate.RoomType.TypeName),
                BaseRent = candidate.RoomType.Price,
                Property = new PublicRoomPropertyReadModel(
                    candidate.Property!.PropertyId,
                    candidate.Property.PropertyName,
                    candidate.Property.AddressText,
                    candidate.Property.Ward,
                    candidate.Property.District,
                    candidate.Property.City)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (room is null)
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return null;
        }

        var amenityRows = await _context.RoomAmenities
            .AsNoTracking()
            .Where(link => link.RoomId == roomId)
            .OrderBy(link => link.AmenityId)
            .Select(link => new PublicAmenitySummaryReadModel(link.Amenity!.AmenityId, link.Amenity.Name))
            .ToListAsync(cancellationToken);
        var amenities = amenityRows
            .DistinctBy(amenity => amenity.AmenityId)
            .ToArray();

        var services = await _context.RoomTypeServices
            .AsNoTracking()
            .Where(link => link.RoomTypeId == room.RoomTypeId)
            .OrderBy(link => link.ServiceId)
            .Select(link => new PublicRoomServiceReadModel(
                link.Service!.ServiceId,
                link.Service.Name,
                link.Service.Price))
            .ToListAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new PublicRoomDetailReadModel(
            room.RoomId,
            room.RoomCode,
            room.AreaSqm,
            room.RoomType,
            room.BaseRent,
            room.Property,
            Array.Empty<string>(),
            amenities,
            services,
            Array.Empty<PublicRoomEquipmentReadModel>());
    }

    private static IQueryable<Rooms> ApplyRoomFilters(
        IQueryable<Rooms> rooms,
        PublicPropertySearchCriteria criteria)
    {
        if (criteria.MinPrice is decimal minPrice)
        {
            rooms = rooms.Where(room => room.RoomType!.Price >= minPrice);
        }
        if (criteria.MaxPrice is decimal maxPrice)
        {
            rooms = rooms.Where(room => room.RoomType!.Price <= maxPrice);
        }
        if (criteria.RoomTypeName is not null)
        {
            var roomTypeName = criteria.RoomTypeName.ToLowerInvariant();
            rooms = rooms.Where(room => room.RoomType!.TypeName.Trim().ToLower() == roomTypeName);
        }

        foreach (var amenityId in criteria.AmenityIds)
        {
            rooms = rooms.Where(room => room.RoomAmenities.Any(link => link.AmenityId == amenityId));
        }

        return rooms;
    }

    private static IQueryable<Property> ApplyPropertyFilters(
        IQueryable<Property> properties,
        PublicPropertySearchCriteria criteria)
    {
        if (criteria.Keyword is not null)
        {
            var keyword = criteria.Keyword.ToLowerInvariant();
            properties = properties.Where(property =>
                property.PropertyName.ToLower().Contains(keyword) ||
                property.AddressText.ToLower().Contains(keyword) ||
                property.Ward.ToLower().Contains(keyword) ||
                property.District.ToLower().Contains(keyword) ||
                property.City.ToLower().Contains(keyword));
        }

        if (criteria.City is not null)
        {
            var city = criteria.City.ToLowerInvariant();
            properties = properties.Where(property => property.City.Trim().ToLower() == city);
        }
        if (criteria.District is not null)
        {
            var district = criteria.District.ToLowerInvariant();
            properties = properties.Where(property => property.District.Trim().ToLower() == district);
        }
        if (criteria.Ward is not null)
        {
            var ward = criteria.Ward.ToLowerInvariant();
            properties = properties.Where(property => property.Ward.Trim().ToLower() == ward);
        }

        return properties;
    }
}
