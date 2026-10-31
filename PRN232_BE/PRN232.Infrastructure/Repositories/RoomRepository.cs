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

    public Task<List<RoomSummaryReadModel>> GetAllAsync(int? propertyId, string? status, string? city, decimal? maxPrice) =>
        _context.Rooms
            .AsNoTracking()
            .Where(r => propertyId == null || r.PropertyId == propertyId)
            .Where(r => status == null || r.Status == status)
            .Where(r => city == null || r.Property!.City == city)
            .Where(r => maxPrice == null || r.RoomType!.Price <= maxPrice)
            .OrderBy(r => r.RoomId)
            .Select(r => new RoomSummaryReadModel(
                r.RoomId,
                r.PropertyId,
                r.Property!.PropertyName,
                r.RoomTypeId,
                r.RoomType!.TypeName,
                r.RoomType.Price,
                r.RoomCode,
                r.AreaSqm,
                r.Status,
                r.RoomImages.Where(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault()))
            .ToListAsync();

    public Task<RoomSummaryReadModel?> GetByIdAsync(int id) =>
        _context.Rooms
            .AsNoTracking()
            .Where(r => r.RoomId == id)
            .Select(r => new RoomSummaryReadModel(
                r.RoomId,
                r.PropertyId,
                r.Property!.PropertyName,
                r.RoomTypeId,
                r.RoomType!.TypeName,
                r.RoomType.Price,
                r.RoomCode,
                r.AreaSqm,
                r.Status,
                r.RoomImages.Where(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault()))
            .FirstOrDefaultAsync();

    public Task<List<RentableRoomReadModel>> GetRentableRoomsAsync(DateTimeOffset asOfUtc) =>
        RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc)
            .OrderBy(room => room.RoomId)
            .Select(room => new RentableRoomReadModel(
                room.RoomId,
                room.PropertyId,
                room.RoomTypeId,
                room.RoomType!.TypeName,
                room.RoomType.Price,
                room.RoomCode,
                room.AreaSqm))
            .ToListAsync();

    public Task<List<PropertyAvailabilityReadModel>> GetPropertyAvailabilityAsync(DateTimeOffset asOfUtc) =>
        RoomAvailabilityQueries
            .GetPropertyAvailability(_context.Properties, _context.Rooms, asOfUtc)
            .ToListAsync();

    public Task<Rooms?> GetTrackedByIdAsync(int id) =>
        _context.Rooms.FirstOrDefaultAsync(r => r.RoomId == id);

    public async Task<Rooms> AddAsync(Rooms room)
    {
        await _context.Rooms.AddAsync(room);
        return room;
    }

    public void Remove(Rooms room) => _context.Rooms.Remove(room);

    public Task<int?> GetOwnerIdAsync(int roomId) =>
        _context.Rooms
            .AsNoTracking()
            .Where(r => r.RoomId == roomId)
            .Select(r => (int?)r.Property!.OwnerId)
            .FirstOrDefaultAsync();

    public Task<bool> PropertyExistsAsync(int propertyId) => _context.Properties.AnyAsync(p => p.PropertyId == propertyId);

    public Task<int?> GetPropertyOwnerIdAsync(int propertyId) =>
        _context.Properties
            .AsNoTracking()
            .Where(p => p.PropertyId == propertyId)
            .Select(p => (int?)p.OwnerId)
            .FirstOrDefaultAsync();

    public Task<bool> RoomTypeExistsAsync(int roomTypeId) => _context.RoomTypes.AnyAsync(rt => rt.RoomTypeId == roomTypeId);

    public async Task<bool> HasDependentsAsync(int roomId) =>
        await _context.Equipments.AnyAsync(e => e.RoomId == roomId) ||
        await _context.Contracts.AnyAsync(c => c.RoomId == roomId) ||
        await _context.Bookings.AnyAsync(b => b.RoomId == roomId);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    public Task<List<RoomImageReadModel>> GetImagesAsync(int roomId) =>
        _context.RoomImages
            .AsNoTracking()
            .Where(i => i.RoomId == roomId)
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.RoomImageId)
            .Select(i => new RoomImageReadModel(i.RoomImageId, i.RoomId, i.ImageUrl, i.IsPrimary))
            .ToListAsync();

    public async Task<RoomImages> AddImageAsync(int roomId, RoomImages image)
    {
        if (image.IsPrimary)
        {
            // chỉ 1 ảnh primary / phòng — gỡ cờ primary của mấy ảnh cũ trước khi thêm ảnh mới
            await _context.RoomImages
                .Where(i => i.RoomId == roomId && i.IsPrimary)
                .ExecuteUpdateAsync(update => update.SetProperty(i => i.IsPrimary, false));
        }

        image.RoomId = roomId;
        await _context.RoomImages.AddAsync(image);
        return image;
    }

    public async Task<bool> RemoveImageAsync(int roomId, int imageId)
    {
        var image = await _context.RoomImages.FirstOrDefaultAsync(i => i.RoomId == roomId && i.RoomImageId == imageId);
        if (image is null)
        {
            return false;
        }
        _context.RoomImages.Remove(image);
        await _context.SaveChangesAsync();
        return true;
    }

    public Task<List<Amenities>> GetAmenitiesAsync(int roomId) =>
        _context.RoomAmenities
            .AsNoTracking()
            .Where(link => link.RoomId == roomId)
            .Select(link => link.Amenity!)
            .ToListAsync();

    public Task<bool> AmenityLinkExistsAsync(int roomId, int amenityId) =>
        _context.RoomAmenities.AnyAsync(link => link.RoomId == roomId && link.AmenityId == amenityId);

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

    public Task<bool> AmenityExistsAsync(int amenityId) => _context.Amenities.AnyAsync(a => a.AmenityId == amenityId);
}
