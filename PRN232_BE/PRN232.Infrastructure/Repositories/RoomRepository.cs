using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly AppDbContext _context;

    public RoomRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<RoomSummaryReadModel>> GetAllAsync(
        int? propertyId,
        string? status,
        string? city,
        decimal? maxPrice,
        int? managerId = null,
        int? roomTypeId = null,
        string? search = null)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToLower();

        return _context.Rooms
            .AsNoTracking()
            .Where(r => propertyId == null || r.PropertyId == propertyId)
            .Where(r => status == null || r.Status == status)
            .Where(r => city == null || r.Property!.City == city)
            .Where(r => maxPrice == null || r.PropertyRoomType!.MonthlyRent <= maxPrice)
            .Where(r => roomTypeId == null || r.RoomTypeId == roomTypeId)
            .Where(r => normalizedSearch == null || r.RoomCode.ToLower().Contains(normalizedSearch))
            .Where(r => managerId == null || r.Property!.ManagerAssignments.Any(
                assignment => assignment.ManagerId == managerId &&
                    assignment.Manager!.IsActive &&
                    assignment.Manager.Role!.RoleName == PRN232.Application.States.RoleNames.Manager))
            .OrderBy(r => r.RoomId)
            .Select(r => new RoomSummaryReadModel(
            r.RoomId,
            r.PropertyId,
            r.Property!.PropertyName,
            r.RoomTypeId,
            r.RoomType!.TypeName,
            r.PropertyRoomType!.MonthlyRent,
            r.RoomCode,
            r.AreaSqm,
            r.Status,
            r.RoomImages.Where(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault()))
            .ToListAsync();
    }

    public Task<RoomSummaryReadModel?> GetByIdAsync(int id, int? managerId = null)
    {
        return _context.Rooms
            .AsNoTracking()
            .Where(r => r.RoomId == id)
            .Where(r => managerId == null || r.Property!.ManagerAssignments.Any(
                assignment => assignment.ManagerId == managerId &&
                    assignment.Manager!.IsActive &&
                    assignment.Manager.Role!.RoleName == PRN232.Application.States.RoleNames.Manager))
            .Select(r => new RoomSummaryReadModel(
            r.RoomId,
            r.PropertyId,
            r.Property!.PropertyName,
            r.RoomTypeId,
            r.RoomType!.TypeName,
            r.PropertyRoomType!.MonthlyRent,
            r.RoomCode,
            r.AreaSqm,
            r.Status,
            r.RoomImages.Where(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault()))
            .FirstOrDefaultAsync();
    }

    public Task<List<RentableRoomReadModel>> GetRentableRoomsAsync(DateTimeOffset asOfUtc)
    {
        return RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc)
            .OrderBy(room => room.RoomId)
            .Select(room => new RentableRoomReadModel(
            room.RoomId,
            room.PropertyId,
            room.RoomTypeId,
            room.RoomType!.TypeName,
            room.PropertyRoomType!.MonthlyRent,
            room.RoomCode,
            room.AreaSqm))
            .ToListAsync();
    }

    public Task<List<PropertyAvailabilityReadModel>> GetPropertyAvailabilityAsync(DateTimeOffset asOfUtc)
    {
        return RoomAvailabilityQueries
            .GetPropertyAvailability(_context.Properties, _context.Rooms, asOfUtc)
            .ToListAsync();
    }

    public Task<Rooms?> GetTrackedByIdAsync(int id)
    {
        return _context.Rooms.FirstOrDefaultAsync(r => r.RoomId == id);
    }

    public async Task<Rooms> AddAsync(Rooms room)
    {
        await _context.Rooms.AddAsync(room);
        return room;
    }

    public void Remove(Rooms room)
    {
        _context.Rooms.Remove(room);
    }

    public Task<int?> GetOwnerIdAsync(int roomId)
    {
        return _context.Rooms
            .AsNoTracking()
            .Where(r => r.RoomId == roomId)
            .Select(r => (int?)r.Property!.OwnerId)
            .FirstOrDefaultAsync();
    }

    public Task<bool> PropertyExistsAsync(int propertyId)
    {
        return _context.Properties.AnyAsync(p => p.PropertyId == propertyId);
    }

    public Task<int?> GetPropertyOwnerIdAsync(int propertyId)
    {
        return _context.Properties
            .AsNoTracking()
            .Where(p => p.PropertyId == propertyId)
            .Select(p => (int?)p.OwnerId)
            .FirstOrDefaultAsync();
    }

    public Task<bool> RoomTypeExistsAsync(int roomTypeId)
    {
        return _context.RoomTypes.AnyAsync(rt => rt.RoomTypeId == roomTypeId);
    }

    public Task<bool> RoomCodeExistsAsync(
        int propertyId,
        string roomCode,
        int? excludedRoomId = null)
    {
        var normalizedRoomCode = roomCode.Trim().ToLower();
        return _context.Rooms.AnyAsync(room =>
            room.PropertyId == propertyId &&
            room.RoomCode.ToLower() == normalizedRoomCode &&
            (excludedRoomId == null || room.RoomId != excludedRoomId));
    }

    public async Task<bool> HasDependentsAsync(int roomId)
    {
        return await _context.Equipments.AnyAsync(equipment => equipment.RoomId == roomId) ||
            await _context.Contracts.AnyAsync(contract => contract.RoomId == roomId) ||
            await _context.Bookings.AnyAsync(booking => booking.RoomId == roomId) ||
            await _context.RoomImages.AnyAsync(image => image.RoomId == roomId) ||
            await _context.RoomAmenities.AnyAsync(amenity => amenity.RoomId == roomId) ||
            await _context.ViewingSchedules.AnyAsync(viewing => viewing.RoomId == roomId);
    }

    public async Task<bool> HasBookingsOrContractsAsync(int roomId)
    {
        return await _context.Bookings.AnyAsync(booking => booking.RoomId == roomId) ||
            await _context.Contracts.AnyAsync(contract => contract.RoomId == roomId);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    public Task<List<RoomImageReadModel>> GetImagesAsync(int roomId)
    {
        return _context.RoomImages
            .AsNoTracking()
            .Where(i => i.RoomId == roomId)
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.RoomImageId)
            .Select(i => new RoomImageReadModel(i.RoomImageId, i.RoomId, i.ImageUrl, i.IsPrimary))
            .ToListAsync();
    }

    public async Task<RoomImages> AddImageAsync(int roomId, RoomImages image)
    {
        var images = await _context.RoomImages.Where(i => i.RoomId == roomId).ToListAsync();
        image.IsPrimary = image.IsPrimary || !images.Any(i => i.IsPrimary);
        if (image.IsPrimary)
        {
            foreach (var existing in images)
            {
                existing.IsPrimary = false;
            }
        }

        image.RoomId = roomId;
        await _context.RoomImages.AddAsync(image);
        return image;
    }

    public async Task<bool> SetPrimaryImageAsync(int roomId, int imageId)
    {
        var images = await _context.RoomImages.Where(i => i.RoomId == roomId).ToListAsync();
        if (!images.Any(i => i.RoomImageId == imageId))
        {
            return false;
        }
        foreach (var image in images)
        {
            image.IsPrimary = image.RoomImageId == imageId;
        }
        await _context.SaveChangesAsync();
        return true;
    }

    public Task<bool> IsImageUrlInUseAsync(string imageUrl, int excludedImageId)
    {
        return _context.RoomImages.AnyAsync(image => image.ImageUrl == imageUrl && image.RoomImageId != excludedImageId);
    }

    public async Task<bool> RemoveImageAsync(int roomId, int imageId)
    {
        var image = await _context.RoomImages.FirstOrDefaultAsync(i => i.RoomId == roomId && i.RoomImageId == imageId);
        if (image is null)
        {
            return false;
        }
        _context.RoomImages.Remove(image);
        if (image.IsPrimary)
        {
            var replacement = await _context.RoomImages
                .Where(i => i.RoomId == roomId && i.RoomImageId != imageId)
                .OrderBy(i => i.RoomImageId)
                .FirstOrDefaultAsync();
            if (replacement is not null)
            {
                replacement.IsPrimary = true;
            }
        }
        await _context.SaveChangesAsync();
        return true;
    }

    public Task<List<Amenities>> GetAmenitiesAsync(int roomId)
    {
        return _context.RoomAmenities
            .AsNoTracking()
            .Where(link => link.RoomId == roomId)
            .Select(link => link.Amenity!)
            .ToListAsync();
    }

    public Task<bool> AmenityLinkExistsAsync(int roomId, int amenityId)
    {
        return _context.RoomAmenities.AnyAsync(link => link.RoomId == roomId && link.AmenityId == amenityId);
    }

    public async Task AddAmenityLinkAsync(int roomId, int amenityId)
    {
        await _context.RoomAmenities.AddAsync(new RoomAmenities { RoomId = roomId, AmenityId = amenityId });
        await _context.SaveChangesAsync();
    }

    public async Task<bool> RemoveAmenityLinkAsync(int roomId, int amenityId)
    {
        var link = await _context.RoomAmenities
            .FirstOrDefaultAsync(l => l.RoomId == roomId && l.AmenityId == amenityId);
        if (link is null)
        {
            return false;
        }
        _context.RoomAmenities.Remove(link);
        await _context.SaveChangesAsync();
        return true;
    }

    public Task<bool> AmenityExistsAsync(int amenityId)
    {
        return _context.Amenities.AnyAsync(a => a.AmenityId == amenityId);
    }
}
